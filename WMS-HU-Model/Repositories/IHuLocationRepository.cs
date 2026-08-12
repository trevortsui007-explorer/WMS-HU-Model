using System.Collections.Generic;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public interface IHuLocationRepository
    {
        HuLocationDto GetLocation(
            long huId);

        HuMoveLogDto GetMoveByRequestId(
            string requestId);

        bool WarehouseAllowsPlant(
            string warehouseCode,
            string plantCode);

        int CountDifferentPlantAtLocation(
            long locationId,
            string plantCode);

        MovePersistenceResult ExecuteMove(
            HuMoveCommand command);

        List<HuMoveLogDto> GetMoveLogs(
            long huId,
            int top);
    }
}
