using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Services
{
    public interface IInboundConfirmService
    {
        OperationResult<InboundConfirmPalletDto> GetPallet(string palletHuCode);
        OperationResult<InboundConfirmPalletDto> ConfirmBox(InboundConfirmBoxRequest request);
    }
}
