using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class InboundConfirmService : IInboundConfirmService
    {
        private readonly IInboundConfirmRepository _repository;

        public InboundConfirmService(IInboundConfirmRepository repository)
        {
            _repository = repository;
        }

        public OperationResult<InboundConfirmPalletDto> GetPallet(string palletHuCode)
        {
            if (!Valid(palletHuCode, 100)) return OperationResult<InboundConfirmPalletDto>.Fail("请扫描有效托盘码（最多100字符）。");
            return _repository.GetPallet(palletHuCode.Trim());
        }

        public OperationResult<InboundConfirmPalletDto> ConfirmBox(InboundConfirmBoxRequest request)
        {
            if (request == null) return OperationResult<InboundConfirmPalletDto>.Fail("请求不能为空。");
            if (!Valid(request.PalletHuCode, 100) || !Valid(request.BoxHuCode, 100))
                return OperationResult<InboundConfirmPalletDto>.Fail("托盘码、箱码不能为空且不能超过100字符。");
            if (!Valid(request.RequestId, 100) || !Valid(request.Operator, 50))
                return OperationResult<InboundConfirmPalletDto>.Fail("请求标识（最多100字符）和操作人（最多50字符）不能为空。");
            string method = (request.ConfirmMethod ?? "").Trim().ToUpperInvariant();
            if (method != "SCANNED" && method != "MANUAL")
                return OperationResult<InboundConfirmPalletDto>.Fail("确认方式必须为SCANNED或MANUAL。");
            return _repository.ConfirmBox(new InboundConfirmBoxRequest
            {
                PalletHuCode = request.PalletHuCode.Trim(), BoxHuCode = request.BoxHuCode.Trim(),
                ConfirmMethod = method, RequestId = request.RequestId.Trim(), Operator = request.Operator.Trim()
            });
        }

        private static bool Valid(string value, int maxLength)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;
        }
    }
}
