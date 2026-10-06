using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public interface IPackWeekcodeRepository
    {
        OperationResult<PackWeekcodeConfig> GetConfig(WeekCodeResolveContext context);
        OperationResult<WeekCodeSnapshot> Resolve(WeekCodeResolveContext context);
    }
}
