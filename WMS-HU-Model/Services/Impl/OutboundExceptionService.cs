using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Services
{
    public class OutboundExceptionService : IOutboundExceptionService
    {
        private readonly IOutboundExceptionRepository _repository;

        public OutboundExceptionService(IOutboundExceptionRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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
