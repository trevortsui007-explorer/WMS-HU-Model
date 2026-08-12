using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Services
{
    public class OutboundService : IOutboundService
    {
        private readonly IOutboundRepository _repository;

        public OutboundService(IOutboundRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public OperationResult<List<OutboundQueueItemDto>> GetQueue(OutboundQueueQuery query)
        {
            if (query == null)
                return OperationResult<List<OutboundQueueItemDto>>.Fail("查询条件不能为空。");

            string errorMessage = ValidateAndNormalizeQueueQuery(query);

            if (!string.IsNullOrEmpty(errorMessage))
                return OperationResult<List<OutboundQueueItemDto>>.Fail(errorMessage);

            List<OutboundQueueItemDto> list = _repository.GetQueue(query);

            return OperationResult<List<OutboundQueueItemDto>>.Success(list);
        }

        public OperationResult<PickResultDto> PickNext(PickNextRequest request)
        {
            if (request == null)
                return OperationResult<PickResultDto>.Fail("请求不能为空。");

            string errorMessage = ValidateAndNormalizePickNextRequest(request);

            if (!string.IsNullOrEmpty(errorMessage))
                return OperationResult<PickResultDto>.Fail(errorMessage);

            /*
             * Service 层快速幂等检查。
             * Repository 事务内部仍会再次检查，
             * Service 层检查只是减少不必要的事务。
             */
            PickResultDto duplicate = _repository.GetPickByRequestId(request.RequestId);

            if (duplicate != null)
            {
                duplicate.IsDuplicate = true;
                return OperationResult<PickResultDto>.Success(duplicate, "重复请求，返回原拣货结果");
            }

            /*
             * 防止 RequestId 已经被 Putaway、Repack、
             * PickBatch 等其他业务使用。
             */
            if (_repository.IsRequestIdUsedByOtherOperation(request.RequestId))
                return OperationResult<PickResultDto>.Fail("RequestId已经被其他WMS业务使用。");

            PickNextCommand command = BuildPickNextCommand(request);
            PickPersistenceResult result = _repository.ExecutePickNext(command);

            if (!result.IsSuccess)
                return OperationResult<PickResultDto>.Fail(result.Message);

            string successMessage = result.Data != null && result.Data.IsDuplicate
                ? "重复请求，返回原拣货结果"
                : "FIFO拣货成功";

            return OperationResult<PickResultDto>.Success(result.Data, successMessage);
        }

        public OperationResult<OutboundBatchPlanDto> PlanBatch(OutboundBatchPlanRequest request)
        {
            if (request == null)
                return OperationResult<OutboundBatchPlanDto>.Fail("请求不能为空。");

            string errorMessage = ValidateAndNormalizeBatchPlanRequest(request);

            if (!string.IsNullOrEmpty(errorMessage))
                return OperationResult<OutboundBatchPlanDto>.Fail(errorMessage);

            List<OutboundPickCandidateDto> candidates = _repository.GetCandidates(request);

            OutboundBatchPlanDto plan = OutboundFifoPlanner.Build(
                candidates,
                request.RequiredQty,
                request.Uom,
                request.AllowOverPick);

            return OperationResult<OutboundBatchPlanDto>.Success(plan);
        }

        public OperationResult<PickBatchResultDto> PickBatch(PickBatchRequest request)
        {
            if (request == null)
                return OperationResult<PickBatchResultDto>.Fail("请求不能为空。");

            string errorMessage = ValidateAndNormalizePickBatchRequest(request);

            if (!string.IsNullOrEmpty(errorMessage))
                return OperationResult<PickBatchResultDto>.Fail(errorMessage);

            /*
             * 先检查是否已经存在同一个 PickBatch。
             */
            PickBatchResultDto duplicate = _repository.GetPickBatchByRequestId(request.RequestId);

            if (duplicate != null)
            {
                if (!IsSamePickBatchRequest(duplicate, request))
                    return OperationResult<PickBatchResultDto>.Fail("RequestId已经被其他批量拣货业务使用。");

                duplicate.IsDuplicate = true;

                return OperationResult<PickBatchResultDto>.Success(
                    duplicate,
                    "重复请求，返回原批量拣货结果");
            }

            /*
             * 再检查 RequestId 是否被其他业务使用。
             */
            if (_repository.IsRequestIdUsedByOtherOperation(request.RequestId))
                return OperationResult<PickBatchResultDto>.Fail("RequestId已经被其他WMS业务使用。");

            PickBatchCommand command = BuildPickBatchCommand(request);
            PickBatchPersistenceResult result = _repository.ExecutePickBatch(command);

            if (!result.IsSuccess)
                return OperationResult<PickBatchResultDto>.Fail(result.Message);

            string successMessage = result.Data != null && result.Data.IsDuplicate
                ? "重复请求，返回原批量拣货结果"
                : "FIFO批量拣货成功";

            return OperationResult<PickBatchResultDto>.Success(result.Data, successMessage);
        }

        private static string ValidateAndNormalizeQueueQuery(OutboundQueueQuery query)
        {
            if (string.IsNullOrWhiteSpace(query.WarehouseCode))
                return "WarehouseCode不能为空。";

            if (string.IsNullOrWhiteSpace(query.PlantCode))
                return "PlantCode不能为空。";

            if (string.IsNullOrWhiteSpace(query.MaterialCode))
                return "MaterialCode不能为空。";

            query.WarehouseCode = NormalizeUpper(query.WarehouseCode);
            query.PlantCode = NormalizeUpper(query.PlantCode);
            query.MaterialCode = NormalizeUpper(query.MaterialCode);
            query.MaterialRevision = NormalizeOptionalUpper(query.MaterialRevision);
            query.BatchNo = NormalizeOptionalUpper(query.BatchNo);
            query.ProductionOrderNo = NormalizeOptionalUpper(query.ProductionOrderNo);

            if (query.Top <= 0)
                query.Top = 20;

            if (query.Top > 200)
                query.Top = 200;

            return null;
        }

        private static string ValidateAndNormalizePickNextRequest(PickNextRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WarehouseCode))
                return "WarehouseCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.PlantCode))
                return "PlantCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.MaterialCode))
                return "MaterialCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.RequestId))
                return "RequestId不能为空。";

            if (string.IsNullOrWhiteSpace(request.Operator))
                return "Operator不能为空。";

            request.WarehouseCode = NormalizeUpper(request.WarehouseCode);
            request.PlantCode = NormalizeUpper(request.PlantCode);
            request.MaterialCode = NormalizeUpper(request.MaterialCode);
            request.MaterialRevision = NormalizeOptionalUpper(request.MaterialRevision);
            request.BatchNo = NormalizeOptionalUpper(request.BatchNo);
            request.ProductionOrderNo = NormalizeOptionalUpper(request.ProductionOrderNo);
            request.RequestId = NormalizeText(request.RequestId);
            request.BusinessNo = NormalizeOptionalText(request.BusinessNo);
            request.Operator = NormalizeText(request.Operator);

            return null;
        }

        private static string ValidateAndNormalizeBatchPlanRequest(OutboundBatchPlanRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WarehouseCode))
                return "WarehouseCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.PlantCode))
                return "PlantCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.MaterialCode))
                return "MaterialCode不能为空。";

            if (request.RequiredQty <= 0)
                return "RequiredQty必须大于0。";

            request.WarehouseCode = NormalizeUpper(request.WarehouseCode);
            request.PlantCode = NormalizeUpper(request.PlantCode);
            request.MaterialCode = NormalizeUpper(request.MaterialCode);
            request.MaterialRevision = NormalizeOptionalUpper(request.MaterialRevision);
            request.BatchNo = NormalizeOptionalUpper(request.BatchNo);
            request.ProductionOrderNo = NormalizeOptionalUpper(request.ProductionOrderNo);
            request.Uom = string.IsNullOrWhiteSpace(request.Uom) ? "PCS" : NormalizeUpper(request.Uom);

            return null;
        }

        private static string ValidateAndNormalizePickBatchRequest(PickBatchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WarehouseCode))
                return "WarehouseCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.PlantCode))
                return "PlantCode不能为空。";

            if (string.IsNullOrWhiteSpace(request.MaterialCode))
                return "MaterialCode不能为空。";

            if (request.RequiredQty <= 0)
                return "RequiredQty必须大于0。";

            if (string.IsNullOrWhiteSpace(request.RequestId))
                return "RequestId不能为空。";

            if (string.IsNullOrWhiteSpace(request.Operator))
                return "Operator不能为空。";

            request.WarehouseCode = NormalizeUpper(request.WarehouseCode);
            request.PlantCode = NormalizeUpper(request.PlantCode);
            request.MaterialCode = NormalizeUpper(request.MaterialCode);
            request.MaterialRevision = NormalizeOptionalUpper(request.MaterialRevision);
            request.BatchNo = NormalizeOptionalUpper(request.BatchNo);
            request.ProductionOrderNo = NormalizeOptionalUpper(request.ProductionOrderNo);
            request.Uom = string.IsNullOrWhiteSpace(request.Uom) ? "PCS" : NormalizeUpper(request.Uom);
            request.RequestId = NormalizeText(request.RequestId);
            request.BusinessNo = NormalizeOptionalText(request.BusinessNo);
            request.Operator = NormalizeText(request.Operator);

            return null;
        }

        private static PickNextCommand BuildPickNextCommand(PickNextRequest request)
        {
            return new PickNextCommand
            {
                WarehouseCode = request.WarehouseCode,
                PlantCode = request.PlantCode,
                MaterialCode = request.MaterialCode,
                MaterialRevision = request.MaterialRevision,
                BatchNo = request.BatchNo,
                ProductionOrderNo = request.ProductionOrderNo,
                OperationNo = GenerateOperationNo(),
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };
        }

        private static PickBatchCommand BuildPickBatchCommand(PickBatchRequest request)
        {
            return new PickBatchCommand
            {
                PickBatchNo = GeneratePickBatchNo(),
                WarehouseCode = request.WarehouseCode,
                PlantCode = request.PlantCode,
                MaterialCode = request.MaterialCode,
                MaterialRevision = request.MaterialRevision,
                BatchNo = request.BatchNo,
                ProductionOrderNo = request.ProductionOrderNo,
                RequiredQty = request.RequiredQty,
                Uom = request.Uom,
                AllowOverPick = request.AllowOverPick,
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };
        }

        private static bool IsSamePickBatchRequest(PickBatchResultDto existing, PickBatchRequest current)
        {
            return string.Equals(
                       existing.WarehouseCode,
                       current.WarehouseCode,
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   string.Equals(
                       existing.PlantCode,
                       current.PlantCode,
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   string.Equals(
                       existing.MaterialCode,
                       current.MaterialCode,
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   existing.RequiredQty == current.RequiredQty
                   &&
                   string.Equals(
                       existing.Uom,
                       current.Uom,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string GenerateOperationNo()
        {
            return Guid.NewGuid()
                .ToString("N")
                .ToUpperInvariant();
        }

        private static string GeneratePickBatchNo()
        {
            return "PB" +
                   DateTime.Now.ToString("yyyyMMddHHmmss") +
                   "_" +
                   Guid.NewGuid()
                       .ToString("N")
                       .Substring(0, 8)
                       .ToUpperInvariant();
        }

        private static string NormalizeUpper(string value)
        {
            return value.Trim().ToUpperInvariant();
        }

        private static string NormalizeOptionalUpper(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string value)
        {
            return value.Trim();
        }

        private static string NormalizeOptionalText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}