using System.Collections.Generic;
using WMS_HU_Model.Map.Dtos;
using WMS_HU_Model.Map.Models;

namespace WMS_HU_Model.Map.Repositories
{
    public interface IMapRepository
    {
        List<MapLayout> GetLayouts(string warehouseCode);
        MapLayout GetLayout(long layoutId);
        MapLayout GetRuntimeLayout(string warehouseCode);
        List<MapObject> GetObjects(long layoutId);
        long CreateDraftLayout(CreateMapLayoutRequest request);
        List<MapLocationBinding> GetLocationBindings(long warehouseId);
        void SaveObjects(long layoutId, IList<MapObject> upserts, IList<long> deletedIds, string operatorName);
        void ImportObjects(long layoutId, IList<MapObject> objects, decimal minX, decimal minY, decimal maxX, decimal maxY, string operatorName);
    }
}
