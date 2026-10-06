using System.Collections.Generic;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public interface IHuIdentifierRepository
    {
        List<HuIdentityDto> ResolveCandidates(string rawValue, string customerCode);
    }
}
