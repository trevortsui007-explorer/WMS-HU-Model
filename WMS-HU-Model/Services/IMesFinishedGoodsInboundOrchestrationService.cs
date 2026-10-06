using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Services
{
    public interface IMesFinishedGoodsInboundOrchestrationService
    {
        OperationResult<MesFinishedGoodsInboundTriggerResult> Trigger(string planStockId);
    }
}
