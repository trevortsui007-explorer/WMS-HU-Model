using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound;
using WMS_HU_Model.Repositories;

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
            {
                return OperationResult<List<OutboundQueueItemDto>>.Fail("查询条件不能为空。");
            }

            // 校验并清洗入参
            string errorMessage = ValidateAndNormalizeQuery(query);
            if (errorMessage != null)
            {
                return OperationResult<List<OutboundQueueItemDto>>.Fail(errorMessage);
            }

            List<OutboundQueueItemDto> list = _repository.GetQueue(query);
            return OperationResult<List<OutboundQueueItemDto>>.Success(list);
        }

        public OperationResult<PickResultDto> PickNext(PickNextRequest request)
        {
            if (request == null)
            {
                return OperationResult<PickResultDto>.Fail("请求不能为空。");
            }

            // 1. 校验与标准化参数
            string errorMessage = ValidateAndNormalizeRequest(request);
            if (errorMessage != null)
            {
                return OperationResult<PickResultDto>.Fail(errorMessage);
            }

            // 2. Service 层快速幂等检查 (防重提交)
            PickResultDto duplicate = _repository.GetPickByRequestId(request.RequestId);
            if (duplicate != null)
            {
                duplicate.IsDuplicate = true;
                return OperationResult<PickResultDto>.Success(duplicate, "重复请求，返回原拣货结果");
            }

            // 3. 构建持久化 Command 对象
            PickNextCommand command = MapToCommand(request);

            // 4. 执行核心拣货逻辑
            PickPersistenceResult result = _repository.ExecutePickNext(command);
            if (!result.IsSuccess)
            {
                return OperationResult<PickResultDto>.Fail(result.Message);
            }

            string resultMsg = result.Data?.IsDuplicate == true
                ? "重复请求，返回原拣货结果"
                : "FIFO拣货成功";

            return OperationResult<PickResultDto>.Success(result.Data, resultMsg);
        }

        #region Helper Methods (校验、映射与标准化)

        private static string ValidateAndNormalizeQuery(OutboundQueueQuery query)
        {
            if (string.IsNullOrWhiteSpace(query.WarehouseCode)) return "WarehouseCode不能为空。";
            if (string.IsNullOrWhiteSpace(query.PlantCode)) return "PlantCode不能为空。";
            if (string.IsNullOrWhiteSpace(query.MaterialCode)) return "MaterialCode不能为空。";

            query.WarehouseCode = CleanString(query.WarehouseCode);
            query.PlantCode = CleanString(query.PlantCode);
            query.MaterialCode = CleanString(query.MaterialCode);
            query.MaterialRevision = CleanString(query.MaterialRevision);
            query.BatchNo = CleanString(query.BatchNo);
            query.ProductionOrderNo = CleanString(query.ProductionOrderNo);

            // 限制 Top 页大小，防止大查询撑爆内存
            if (query.Top <= 0) query.Top = 20;
            if (query.Top > 200) query.Top = 200;

            return null;
        }

        private static string ValidateAndNormalizeRequest(PickNextRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WarehouseCode)) return "WarehouseCode不能为空。";
            if (string.IsNullOrWhiteSpace(request.PlantCode)) return "PlantCode不能为空。";
            if (string.IsNullOrWhiteSpace(request.MaterialCode)) return "MaterialCode不能为空。";
            if (string.IsNullOrWhiteSpace(request.RequestId)) return "RequestId不能为空。";
            if (string.IsNullOrWhiteSpace(request.Operator)) return "Operator不能为空。";

            request.WarehouseCode = CleanString(request.WarehouseCode);
            request.PlantCode = CleanString(request.PlantCode);
            request.MaterialCode = CleanString(request.MaterialCode);
            request.MaterialRevision = CleanString(request.MaterialRevision);
            request.BatchNo = CleanString(request.BatchNo);
            request.ProductionOrderNo = CleanString(request.ProductionOrderNo);

            return null;
        }

        private static PickNextCommand MapToCommand(PickNextRequest request)
        {
            return new PickNextCommand
            {
                WarehouseCode = request.WarehouseCode,
                PlantCode = request.PlantCode,
                MaterialCode = request.MaterialCode,
                MaterialRevision = request.MaterialRevision,
                BatchNo = request.BatchNo,
                ProductionOrderNo = request.ProductionOrderNo,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };
        }

        /// <summary>
        /// 统一对字符串去除首尾空格并转大写
        /// </summary>
        private static string CleanString(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? input : input.Trim().ToUpperInvariant();
        }

        #endregion
    }
}