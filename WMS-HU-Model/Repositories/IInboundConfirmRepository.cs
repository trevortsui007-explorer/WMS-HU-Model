using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Repositories
{
    public interface IInboundConfirmRepository
    {
        OperationResult<InboundConfirmPalletDto> GetPallet(string palletHuCode);
        OperationResult<InboundConfirmPalletDto> ConfirmBox(InboundConfirmBoxRequest request);
    }
}
