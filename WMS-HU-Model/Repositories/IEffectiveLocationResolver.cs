using System.Collections.Generic;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public interface IEffectiveLocationResolver
    {
        HuLocationDto Resolve(long huId);
        List<HuLocationDto> ResolveMany(IList<long> huIds);
        List<long> FindHuIdsAtLocation(long locationId, string huType);
    }
}
