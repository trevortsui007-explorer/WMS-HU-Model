using System;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class HuHoldService : IHuHoldService
    {
        private readonly IHuHoldRepository _repository;

        public HuHoldService(IHuHoldRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public OperationResult<HuHoldResultDto> Hold(string huCode, HoldHuRequest request)
        {
            string code = NormalizeCode(huCode);
            if (code == null)
                return OperationResult<HuHoldResultDto>.Fail("HUCode不能为空。");
            if (request == null)
                return OperationResult<HuHoldResultDto>.Fail("请求不能为空。");

            request.RequestId = NormalizeText(request.RequestId);
            request.Operator = NormalizeText(request.Operator);
            request.HoldType = NormalizeCode(request.HoldType);
            request.ReasonCode = NormalizeCode(request.ReasonCode);
            request.Reason = NormalizeText(request.Reason);
            if (request.RequestId == null || request.Operator == null
                || request.HoldType == null || request.ReasonCode == null)
                return OperationResult<HuHoldResultDto>.Fail("RequestId、Operator、HoldType和ReasonCode不能为空。");
            if (request.RequestId.Length > 100 || request.Operator.Length > 50
                || request.HoldType.Length > 30 || request.ReasonCode.Length > 50)
                return OperationResult<HuHoldResultDto>.Fail("Hold请求字段长度超出数据库限制。");
            if (request.Reason != null && request.Reason.Length > 500)
                return OperationResult<HuHoldResultDto>.Fail("Reason长度不能超过500。");
            return _repository.Hold(code, request);
        }

        public OperationResult<HuHoldResultDto> Release(string huCode, ReleaseHuRequest request)
        {
            string code = NormalizeCode(huCode);
            if (code == null)
                return OperationResult<HuHoldResultDto>.Fail("HUCode不能为空。");
            if (request == null)
                return OperationResult<HuHoldResultDto>.Fail("请求不能为空。");
            request.RequestId = NormalizeText(request.RequestId);
            request.Operator = NormalizeText(request.Operator);
            request.Reason = NormalizeText(request.Reason);
            if (request.RequestId == null || request.Operator == null || request.Reason == null)
                return OperationResult<HuHoldResultDto>.Fail("RequestId、Operator和Reason不能为空。");
            if (request.RequestId.Length > 100 || request.Operator.Length > 50 || request.Reason.Length > 500)
                return OperationResult<HuHoldResultDto>.Fail("Release请求字段长度超出数据库限制。");
            return _repository.Release(code, request);
        }

        private static string NormalizeCode(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
