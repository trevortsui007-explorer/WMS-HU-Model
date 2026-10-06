using WMS_HU_Model.Models.Inbound;
using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Repositories
{
    public interface IMesFinishedGoodsInboundRepository
    {
        MesFinishedGoodsInboundSourceGraph GetPlanStock(string planStockSourceId);
        Dictionary<string, DateTime?> GetPackCreatedTimes(IList<string> packSourceEntityIds);
    }
}
