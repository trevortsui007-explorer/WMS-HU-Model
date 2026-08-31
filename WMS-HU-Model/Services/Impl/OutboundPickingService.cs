using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Services
{
    public class OutboundPickingService : IOutboundPickingService
    {
        private readonly IOutboundPickingRepository _repository;
        private readonly IOutboundOrderRepository _orderRepository;
        private readonly IHuIdentifierService _identifierService;

        public OutboundPickingService(
            IOutboundPickingRepository repository,
            IOutboundOrderRepository orderRepository,
            IHuIdentifierService identifierService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _identifierService = identifierService ?? throw new ArgumentNullException(nameof(identifierService));
        }

        public OperationResult<OutboundPreviewDto> Preview(string sapDeliveryNo)
        {
            string deliveryNo = NormalizeRequired(sapDeliveryNo);
            if (deliveryNo == null) return OperationResult<OutboundPreviewDto>.Fail("VBELN不能为空。");
            OutboundOrderDto order = _orderRepository.GetOrder(deliveryNo);
            if (order == null) return OperationResult<OutboundPreviewDto>.Fail("出库订单不存在。");
            if (!string.Equals(order.OrderStatus, "WAIT_PICK", StringComparison.Ordinal)) return OperationResult<OutboundPreviewDto>.Fail("只有WAIT_PICK订单允许执行Preview。");
            if (string.IsNullOrWhiteSpace(order.SAPStorageLocation)) return OperationResult<OutboundPreviewDto>.Fail("订单SAPStorageLocation为空，无法限定LGORT库存范围。");

            OutboundPreviewDto result = new OutboundPreviewDto { SAPDeliveryNo = order.SAPDeliveryNo, OrderStatus = order.OrderStatus };
            foreach (IGrouping<string, OutboundOrderLineDto> group in order.Lines.OrderBy(x => x.SAPItemNo, StringComparer.Ordinal).GroupBy(x => BuildGroupKey(x.MaterialCode, x.Uom), StringComparer.Ordinal))
            {
                OutboundOrderLineDto first = group.First();
                decimal requiredQty = group.Sum(x => x.EffectiveRequiredQty);
                List<OutboundAllocationCandidateDto> candidates = _repository.GetAllocationCandidates(order, first.MaterialCode, first.Uom);
                OutboundAllocationPlanDto plan;
                try { plan = OutboundAllocationPlanner.Build(candidates, requiredQty); }
                catch (InvalidOperationException ex) { return OperationResult<OutboundPreviewDto>.Fail(ex.Message); }
                result.Groups.Add(new OutboundPreviewGroupDto { GroupKey = group.Key, MaterialCode = first.MaterialCode, Uom = first.Uom, RequiredQty = requiredQty, Plan = plan });
            }
            if (result.Groups.Count == 0) return OperationResult<OutboundPreviewDto>.Fail("订单没有可分配的明细行。");
            return OperationResult<OutboundPreviewDto>.Success(result);
        }

        public OperationResult<StartPickingResultDto> StartPicking(string sapDeliveryNo, StartPickingRequest request)
        {
            string deliveryNo = NormalizeRequired(sapDeliveryNo);
            if (deliveryNo == null) return OperationResult<StartPickingResultDto>.Fail("VBELN不能为空。");
            if (request == null) return OperationResult<StartPickingResultDto>.Fail("请求不能为空。");
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.RequestId == null) return OperationResult<StartPickingResultDto>.Fail("RequestId不能为空。");
            if (request.RequestId.Length > 90) return OperationResult<StartPickingResultDto>.Fail("RequestId长度不能超过90。");
            if (request.Operator == null) return OperationResult<StartPickingResultDto>.Fail("Operator不能为空。");
            if (request.Operator.Length > 50) return OperationResult<StartPickingResultDto>.Fail("Operator长度不能超过50。");
            if (request.Remark != null && request.Remark.Length > 500) return OperationResult<StartPickingResultDto>.Fail("Remark长度不能超过500。");
            StartPickingResultDto duplicate = _repository.GetStartPickingByRequestId(request.RequestId);
            if (duplicate != null)
            {
                if (!string.Equals(duplicate.SAPDeliveryNo, deliveryNo, StringComparison.Ordinal)) return OperationResult<StartPickingResultDto>.Fail("RequestId已经被其他出库订单使用。");
                duplicate.IsDuplicate = true;
                return OperationResult<StartPickingResultDto>.Success(duplicate, "重复请求，返回原分配结果。");
            }
            StartPickingPersistenceResult persistence = _repository.ExecuteStartPicking(new StartPickingCommand { SAPDeliveryNo = deliveryNo, RequestId = request.RequestId, Operator = request.Operator, Remark = request.Remark });
            return persistence.IsSuccess ? OperationResult<StartPickingResultDto>.Success(persistence.Data) : OperationResult<StartPickingResultDto>.Fail(persistence.Message);
        }

        public OperationResult<OutboundActionResultDto> ReleaseAllocation(string allocationNo, OutboundActionRequest request)
        {
            string businessNo = NormalizeRequired(allocationNo);
            string error = ValidateActionRequest(request, "AllocationNo", businessNo);
            if (error != null) return OperationResult<OutboundActionResultDto>.Fail(error);
            OutboundActionPersistenceResult result = _repository.ReleaseAllocation(businessNo, request);
            return result.IsSuccess ? OperationResult<OutboundActionResultDto>.Success(result.Data) : OperationResult<OutboundActionResultDto>.Fail(result.Message);
        }

        public OperationResult<OutboundScanResultDto> Scan(string pickBatchNo, OutboundScanRequest request)
        {
            string batchNo = NormalizeRequired(pickBatchNo);
            if (batchNo == null) return OperationResult<OutboundScanResultDto>.Fail("PickBatchNo不能为空。");
            if (request == null) return OperationResult<OutboundScanResultDto>.Fail("请求不能为空。");
            request.HuCodeOrIdentifier = string.IsNullOrWhiteSpace(request.HuCodeOrIdentifier) ? null : request.HuCodeOrIdentifier.Trim();
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            if (request.HuCodeOrIdentifier == null) return OperationResult<OutboundScanResultDto>.Fail("HuCodeOrIdentifier不能为空。");
            if (request.RequestId == null) return OperationResult<OutboundScanResultDto>.Fail("RequestId不能为空。");
            if (request.RequestId.Length > 100) return OperationResult<OutboundScanResultDto>.Fail("RequestId长度不能超过100。");
            if (request.Operator == null) return OperationResult<OutboundScanResultDto>.Fail("Operator不能为空。");
            if (request.Operator.Length > 50) return OperationResult<OutboundScanResultDto>.Fail("Operator长度不能超过50。");
            OutboundScanResultDto duplicate = _repository.GetScanByRequestId(request.RequestId);
            if (duplicate != null)
            {
                if (!string.Equals(duplicate.PickBatchNo, batchNo, StringComparison.Ordinal)) return OperationResult<OutboundScanResultDto>.Fail("RequestId已经被其他PickBatch使用。");
                duplicate.IsDuplicate = true;
                return OperationResult<OutboundScanResultDto>.Success(duplicate, "重复请求，返回原扫码结果。");
            }
            string customerCode = _repository.GetPickBatchCustomerCode(batchNo);
            if (customerCode == null) return OperationResult<OutboundScanResultDto>.Fail("PickBatch不存在。");
            OperationResult<WMS_HU_Model.Models.HU.HuIdentityDto> identity = _identifierService.Resolve(request.HuCodeOrIdentifier, customerCode);
            if (!identity.IsSuccess) return OperationResult<OutboundScanResultDto>.Fail(identity.Message);
            if (!string.Equals(identity.Data.HuType, "OUTER_BOX", StringComparison.Ordinal)) return OperationResult<OutboundScanResultDto>.Fail("正式出库扫码只支持OUTER_BOX。");
            OutboundScanPersistenceResult persistence = _repository.ExecuteScan(new OutboundScanCommand { PickBatchNo = batchNo, HuId = identity.Data.HuId, ScannedValue = request.HuCodeOrIdentifier, RequestId = request.RequestId, Operator = request.Operator, Remark = request.Remark });
            return persistence.IsSuccess ? OperationResult<OutboundScanResultDto>.Success(persistence.Data) : OperationResult<OutboundScanResultDto>.Fail(persistence.Message);
        }

        private static string ValidateActionRequest(OutboundActionRequest request, string businessName, string businessNo)
        {
            if (businessNo == null) return businessName + "不能为空。";
            if (request == null) return "请求不能为空。";
            request.RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim();
            request.Operator = string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            request.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (request.RequestId == null) return "RequestId不能为空。";
            if (request.RequestId.Length > 100) return "RequestId长度不能超过100。";
            if (request.Operator == null) return "Operator不能为空。";
            if (request.Operator.Length > 50) return "Operator长度不能超过50。";
            if (request.Reason == null) return "Reason不能为空。";
            if (request.Reason.Length > 500) return "Reason长度不能超过500。";
            return null;
        }

        private static string BuildGroupKey(string materialCode, string uom) { return NormalizeRequired(materialCode) + "|" + NormalizeRequired(uom); }
        private static string NormalizeRequired(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
    }
}
