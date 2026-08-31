using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class OutboundOrderService : IOutboundOrderService
    {
        private static readonly HashSet<string> QueryStatuses = new HashSet<string>(StringComparer.Ordinal) { "WAIT_PICK", "ALLOCATED", "PICKING", "PICKED", "EXCEPTION", "SHIPPED", "VOID" };
        private readonly IOutboundOrderRepository _repository;
        public OutboundOrderService(IOutboundOrderRepository repository) { _repository = repository ?? throw new ArgumentNullException(nameof(repository)); }
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

        public OperationResult<OutboundPickingStateDto> GetPickingState(string sapDeliveryNo)
        {
            string normalized = NormalizeRequired(sapDeliveryNo);
            if (normalized == null) return OperationResult<OutboundPickingStateDto>.Fail("VBELN不能为空。");
            if (normalized.Length > 30) return OperationResult<OutboundPickingStateDto>.Fail("VBELN长度不能超过30。");
            OutboundPickingStateReadResult state = _repository.GetPickingState(normalized);
            if (state == null) return OperationResult<OutboundPickingStateDto>.Fail("出库单不存在。");
            if (!state.IsSuccess) return OperationResult<OutboundPickingStateDto>.Fail(state.Message);
            return OperationResult<OutboundPickingStateDto>.Success(state.Data);
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
        private static string NormalizeRequired(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
        private static string NormalizeOptionalUpper(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
    }
}
