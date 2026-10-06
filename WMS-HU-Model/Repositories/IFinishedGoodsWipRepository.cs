using System.Collections.Generic;
using WMS_HU_Model.Models.Inventory;

namespace WMS_HU_Model.Repositories
{
    public interface IFinishedGoodsWipRepository
    {
        FinishedGoodsWipDto ReadBox(long boxHUId);
        List<string> ReadFlows(string plantCode, string materialCode, string revision);
        List<FinishedGoodsWipStation> ReadStations(string plantCode, string flowId);
        List<FinishedGoodsWipStation> ReadWip(string plantCode, string materialCode, string revision, string flowId, decimal startSort);
    }
}
