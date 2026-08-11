using System.Collections.Generic;
using WMS_HU_Model.Models.Outbound;

namespace WMS_HU_Model.Repositories
{
    public interface IOutboundRepository
    {
        List<OutboundQueueItemDto> GetQueue(
            OutboundQueueQuery query);


        PickResultDto GetPickByRequestId(
            string requestId);


        PickPersistenceResult ExecutePickNext(
            PickNextCommand command);
    }
}