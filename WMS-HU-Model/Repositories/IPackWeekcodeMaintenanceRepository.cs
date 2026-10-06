using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public interface IPackWeekcodeMaintenanceRepository
    {
        PackWeekcodeConfig Get(string plantCode, string prodNo, string prodRev);
        List<PackWeekcodeFormat> GetSqlFormats();
        string ReadMESWeekCode(string plantCode, string prodNo, string prodRev);
        OperationResult<PackWeekcodeConfig> Save(PackWeekcodeConfig config, string actor);
    }
}
