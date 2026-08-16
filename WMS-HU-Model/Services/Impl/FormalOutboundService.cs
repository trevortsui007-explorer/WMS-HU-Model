using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Services
{
    public class FormalOutboundService : IFormalOutboundService
    {
        private static readonly HashSet<string> QueryStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "WAIT_PICK", "ALLOCATED", "PICKING", "PICKED", "EXCEPTION", "SHIPPED", "VOID"
        };

        private readonly IFormalOutboundRepository _repository;
        private readonly IHuIdentifierService _identifierService;

        public FormalOutboundService(
            IFormalOutboundRepository repository,
            IHuIdentifierService identifierService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _identifierService = identifierService ?? throw new ArgumentNullException(nameof(identifierService));
        }

        public OperationResult<List<OutboundOrderDto>> GetOrders(OutboundOrderQuery query)
        {
            query = query ?? new OutboundOrderQuery();
            query.Status = NormalizeOptionalUpper(query.Status);
            query.Top = query.Top <= 0 ? 50 : Math.Min(query.Top, 200);

            if (query.Status != null && !QueryStatuses.Contains(query.Status))
                return OperationResult<List<OutboundOrderDto>>.Fail("Status不是有效的正式出库订单状态。");

            return OperationResult<List<OutboundOrderDto>>.Success(_repository.GetOrders(query));
        }

        public OperationResult<OutboundOrderDto> GetOrder(string sapDeliveryNo)
        {
            string deliveryNo = NormalizeRequired(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<OutboundOrderDto>.Fail("VBELN不能为空。");

            OutboundOrderDto order = _repository.GetOrder(deliveryNo);
            return order == null
                ? OperationResult<OutboundOrderDto>.Fail("出库订单不存在。")
                : OperationResult<OutboundOrderDto>.Success(order);
        }

        public OperationResult<OutboundPreviewDto> Preview(string sapDeliveryNo)
        {
            string deliveryNo = NormalizeRequired(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<OutboundPreviewDto>.Fail("VBELN不能为空。");

            OutboundOrderDto order = _repository.GetOrder(deliveryNo);
            if (order == null)
                return OperationResult<OutboundPreviewDto>.Fail("出库订单不存在。");

            if (!string.Equals(order.OrderStatus, "WAIT_PICK", StringComparison.Ordinal))
                return OperationResult<OutboundPreviewDto>.Fail("只有WAIT_PICK订单允许执行Preview。");

            if (string.IsNullOrWhiteSpace(order.SAPStorageLocation))
                return OperationResult<OutboundPreviewDto>.Fail("订单SAPStorageLocation为空，无法限定LGORT库存范围。");

            OutboundPreviewDto result = new OutboundPreviewDto
            {
                SAPDeliveryNo = order.SAPDeliveryNo,
                OrderStatus = order.OrderStatus
            };

            foreach (IGrouping<string, OutboundOrderLineDto> group in order.Lines
                .OrderBy(x => x.SAPItemNo, StringComparer.Ordinal)
                .GroupBy(x => BuildGroupKey(x.MaterialCode, x.Uom), StringComparer.Ordinal))
            {
                OutboundOrderLineDto first = group.First();
                decimal requiredQty = group.Sum(x => x.EffectiveRequiredQty);
                List<OutboundAllocationCandidateDto> candidates = _repository.GetAllocationCandidates(
                    order,
                    first.MaterialCode,
                    first.Uom);

                OutboundAllocationPlanDto plan;
                try
                {
                    plan = OutboundAllocationPlanner.Build(candidates, requiredQty);
                }
                catch (InvalidOperationException ex)
                {
                    return OperationResult<OutboundPreviewDto>.Fail(ex.Message);
                }

                result.Groups.Add(new OutboundPreviewGroupDto
                {
                    GroupKey = group.Key,
                    MaterialCode = first.MaterialCode,
                    Uom = first.Uom,
                    RequiredQty = requiredQty,
                    Plan = plan
                });
            }

            if (result.Groups.Count == 0)
                return OperationResult<OutboundPreviewDto>.Fail("订单没有可分配的明细行。");

            return OperationResult<OutboundPreviewDto>.Success(result);
        }

        public OperationResult<StartPickingResultDto> StartPicking(
            string sapDeliveryNo,
            StartPickingRequest request)
        {
            string deliveryNo = NormalizeRequired(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<StartPickingResultDto>.Fail("VBELN不能为空。");

            if (request == null)
                return OperationResult<StartPickingResultDto>.Fail("请求不能为空。");

            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

            if (request.RequestId == null)
                return OperationResult<StartPickingResultDto>.Fail("RequestId不能为空。");
            if (request.RequestId.Length > 90)
                return OperationResult<StartPickingResultDto>.Fail("RequestId长度不能超过90。");
            if (request.Operator == null)
                return OperationResult<StartPickingResultDto>.Fail("Operator不能为空。");
            if (request.Operator.Length > 50)
                return OperationResult<StartPickingResultDto>.Fail("Operator长度不能超过50。");
            if (request.Remark != null && request.Remark.Length > 500)
                return OperationResult<StartPickingResultDto>.Fail("Remark长度不能超过500。");

            StartPickingResultDto duplicate = _repository.GetStartPickingByRequestId(request.RequestId);
            if (duplicate != null)
            {
                if (!string.Equals(duplicate.SAPDeliveryNo, deliveryNo, StringComparison.Ordinal))
                    return OperationResult<StartPickingResultDto>.Fail("RequestId已经被其他出库订单使用。");

                duplicate.IsDuplicate = true;
                return OperationResult<StartPickingResultDto>.Success(duplicate, "重复请求，返回原分配结果。");
            }

            StartPickingPersistenceResult persistence = _repository.ExecuteStartPicking(new StartPickingCommand
            {
                SAPDeliveryNo = deliveryNo,
                RequestId = request.RequestId,
                Operator = request.Operator,
                Remark = request.Remark
            });

            return persistence.IsSuccess
                ? OperationResult<StartPickingResultDto>.Success(persistence.Data)
                : OperationResult<StartPickingResultDto>.Fail(persistence.Message);
        }

        public OperationResult<OutboundActionResultDto> ReleaseAllocation(
            string allocationNo,
            OutboundActionRequest request)
        {
            string businessNo = NormalizeRequired(allocationNo);
            string error = ValidateActionRequest(request, "AllocationNo", businessNo);
            if (error != null)
                return OperationResult<OutboundActionResultDto>.Fail(error);

            OutboundActionPersistenceResult result = _repository.ReleaseAllocation(businessNo, request);
            return result.IsSuccess
                ? OperationResult<OutboundActionResultDto>.Success(result.Data)
                : OperationResult<OutboundActionResultDto>.Fail(result.Message);
        }

        public OperationResult<OutboundActionResultDto> VoidOrder(
            string sapDeliveryNo,
            OutboundActionRequest request)
        {
            string businessNo = NormalizeRequired(sapDeliveryNo);
            string error = ValidateActionRequest(request, "VBELN", businessNo);
            if (error != null)
                return OperationResult<OutboundActionResultDto>.Fail(error);

            OutboundActionPersistenceResult result = _repository.VoidOrder(businessNo, request);
            return result.IsSuccess
                ? OperationResult<OutboundActionResultDto>.Success(result.Data)
                : OperationResult<OutboundActionResultDto>.Fail(result.Message);
        }

        public OperationResult<OutboundScanResultDto> Scan(
            string pickBatchNo,
            OutboundScanRequest request)
        {
            string batchNo = NormalizeRequired(pickBatchNo);
            if (batchNo == null)
                return OperationResult<OutboundScanResultDto>.Fail("PickBatchNo不能为空。");
            if (request == null)
                return OperationResult<OutboundScanResultDto>.Fail("请求不能为空。");

            request.HuCodeOrIdentifier = string.IsNullOrWhiteSpace(request.HuCodeOrIdentifier)
                ? null
                : request.HuCodeOrIdentifier.Trim();
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.HuCodeOrIdentifier == null)
                return OperationResult<OutboundScanResultDto>.Fail("HuCodeOrIdentifier不能为空。");
            if (request.RequestId == null)
                return OperationResult<OutboundScanResultDto>.Fail("RequestId不能为空。");
            if (request.RequestId.Length > 100)
                return OperationResult<OutboundScanResultDto>.Fail("RequestId长度不能超过100。");
            if (request.Operator == null)
                return OperationResult<OutboundScanResultDto>.Fail("Operator不能为空。");
            if (request.Operator.Length > 50)
                return OperationResult<OutboundScanResultDto>.Fail("Operator长度不能超过50。");

            OutboundScanResultDto duplicate = _repository.GetScanByRequestId(request.RequestId);
            if (duplicate != null)
            {
                if (!string.Equals(duplicate.PickBatchNo, batchNo, StringComparison.Ordinal))
                    return OperationResult<OutboundScanResultDto>.Fail("RequestId已经被其他PickBatch使用。");
                duplicate.IsDuplicate = true;
                return OperationResult<OutboundScanResultDto>.Success(duplicate, "重复请求，返回原扫码结果。");
            }

            string customerCode = _repository.GetPickBatchCustomerCode(batchNo);
            if (customerCode == null)
                return OperationResult<OutboundScanResultDto>.Fail("PickBatch不存在。");

            OperationResult<WMS_HU_Model.Models.HU.HuIdentityDto> identity = _identifierService.Resolve(
                request.HuCodeOrIdentifier,
                customerCode);
            if (!identity.IsSuccess)
                return OperationResult<OutboundScanResultDto>.Fail(identity.Message);
            if (!string.Equals(identity.Data.HuType, "OUTER_BOX", StringComparison.Ordinal))
                return OperationResult<OutboundScanResultDto>.Fail("正式出库扫码只支持OUTER_BOX。");

            OutboundScanPersistenceResult persistence = _repository.ExecuteScan(new OutboundScanCommand
            {
                PickBatchNo = batchNo,
                HuId = identity.Data.HuId,
                ScannedValue = request.HuCodeOrIdentifier,
                RequestId = request.RequestId,
                Operator = request.Operator,
                Remark = request.Remark
            });
            return persistence.IsSuccess
                ? OperationResult<OutboundScanResultDto>.Success(persistence.Data)
                : OperationResult<OutboundScanResultDto>.Fail(persistence.Message);
        }

        public OperationResult<OutboundActionResultDto> ShipOrder(
            string sapDeliveryNo,
            ShipOutboundRequest request)
        {
            string deliveryNo = NormalizeRequired(sapDeliveryNo);
            if (deliveryNo == null)
                return OperationResult<OutboundActionResultDto>.Fail("VBELN不能为空。");
            if (request == null)
                return OperationResult<OutboundActionResultDto>.Fail("请求不能为空。");
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.RequestId == null || request.Operator == null)
                return OperationResult<OutboundActionResultDto>.Fail("RequestId和Operator不能为空。");
            if (request.RequestId.Length > 90 || request.Operator.Length > 50)
                return OperationResult<OutboundActionResultDto>.Fail("Ship请求字段长度超出数据库限制。");
            if (request.Remark != null && request.Remark.Length > 500)
                return OperationResult<OutboundActionResultDto>.Fail("Remark长度不能超过500。");

            OutboundActionPersistenceResult result = _repository.ShipOrder(deliveryNo, request);
            return result.IsSuccess
                ? OperationResult<OutboundActionResultDto>.Success(result.Data)
                : OperationResult<OutboundActionResultDto>.Fail(result.Message);
        }

        public OperationResult<OutboundActionResultDto> ReturnPickDetail(
            long pickDetailId,
            ReturnPickDetailRequest request)
        {
            if (pickDetailId <= 0)
                return OperationResult<OutboundActionResultDto>.Fail("PickDetailId必须大于0。");
            if (request == null)
                return OperationResult<OutboundActionResultDto>.Fail("请求不能为空。");
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.ExceptionType = NormalizeRequired(request.ExceptionType);
            request.ReasonCode = NormalizeRequired(request.ReasonCode);
            request.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.RequestId == null || request.Operator == null
                || request.ExceptionType == null || request.ReasonCode == null)
                return OperationResult<OutboundActionResultDto>.Fail(
                    "RequestId、Operator、ExceptionType和ReasonCode不能为空。");
            if (request.RequestId.Length > 100 || request.Operator.Length > 50
                || request.ExceptionType.Length > 30 || request.ReasonCode.Length > 50)
                return OperationResult<OutboundActionResultDto>.Fail("异常退回请求字段长度超出数据库限制。");

            string[] allowedReasons =
            {
                "QUALITY_FAILED", "ORDER_CHANGED", "WRONG_PICK", "MANUAL_EXCEPTION"
            };
            if (!allowedReasons.Contains(request.ReasonCode, StringComparer.Ordinal))
                return OperationResult<OutboundActionResultDto>.Fail("ReasonCode不是已确认的异常原因。");

            OutboundActionPersistenceResult result = _repository.ReturnPickDetail(pickDetailId, request);
            return result.IsSuccess
                ? OperationResult<OutboundActionResultDto>.Success(result.Data)
                : OperationResult<OutboundActionResultDto>.Fail(result.Message);
        }

        public OperationResult<OutboundActionResultDto> ReturnToStock(
            string huCode,
            ReturnToStockRequest request)
        {
            string code = NormalizeRequired(huCode);
            if (code == null)
                return OperationResult<OutboundActionResultDto>.Fail("HUCode不能为空。");
            if (request == null)
                return OperationResult<OutboundActionResultDto>.Fail("请求不能为空。");
            request.ToLocationCode = NormalizeOptionalUpper(request.ToLocationCode);
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.RequestId == null || request.Operator == null)
                return OperationResult<OutboundActionResultDto>.Fail("RequestId和Operator不能为空。");
            if (request.RequestId.Length > 100 || request.Operator.Length > 50)
                return OperationResult<OutboundActionResultDto>.Fail("ReturnToStock请求字段长度超出数据库限制。");
            if (request.Remark != null && request.Remark.Length > 500)
                return OperationResult<OutboundActionResultDto>.Fail("Remark长度不能超过500。");

            OutboundActionPersistenceResult result = _repository.ReturnToStock(code, request);
            return result.IsSuccess
                ? OperationResult<OutboundActionResultDto>.Success(result.Data)
                : OperationResult<OutboundActionResultDto>.Fail(result.Message);
        }

        public OperationResult<StartPickingResultDto> ReserveReplacement(
            string pickBatchNo,
            ReplacementRequest request)
        {
            string batchNo = NormalizeRequired(pickBatchNo);
            if (batchNo == null)
                return OperationResult<StartPickingResultDto>.Fail("PickBatchNo不能为空。");
            if (request == null)
                return OperationResult<StartPickingResultDto>.Fail("请求不能为空。");
            request.ExceptionNo = NormalizeRequired(request.ExceptionNo);
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.ExceptionNo == null || request.RequestId == null || request.Operator == null)
                return OperationResult<StartPickingResultDto>.Fail("ExceptionNo、RequestId和Operator不能为空。");
            if (request.ExceptionNo.Length > 50 || request.RequestId.Length > 100
                || request.Operator.Length > 50)
                return OperationResult<StartPickingResultDto>.Fail("Replacement请求字段长度超出数据库限制。");

            StartPickingPersistenceResult result = _repository.ReserveReplacement(batchNo, request);
            return result.IsSuccess
                ? OperationResult<StartPickingResultDto>.Success(result.Data)
                : OperationResult<StartPickingResultDto>.Fail(result.Message);
        }

        private static string ValidateActionRequest(
            OutboundActionRequest request,
            string businessName,
            string businessNo)
        {
            if (businessNo == null)
                return businessName + "不能为空。";
            if (request == null)
                return "请求不能为空。";

            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (request.RequestId == null)
                return "RequestId不能为空。";
            if (request.RequestId.Length > 100)
                return "RequestId长度不能超过100。";
            if (request.Operator == null)
                return "Operator不能为空。";
            if (request.Operator.Length > 50)
                return "Operator长度不能超过50。";
            if (request.Reason == null)
                return "Reason不能为空。";
            if (request.Reason.Length > 500)
                return "Reason长度不能超过500。";
            return null;
        }

        private static string BuildGroupKey(string materialCode, string uom)
        {
            return NormalizeRequired(materialCode) + "|" + NormalizeRequired(uom);
        }

        private static string NormalizeRequired(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        private static string NormalizeOptionalUpper(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }
    }
}
