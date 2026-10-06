using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Repositories
{
    public interface IMesWeekCodeRuleRepository
    {
        MesWeekCodeRuleReadResult Read(string plantCode, string prodNo, string prodRev);
        System.Collections.Generic.List<MesWeekCodeVersionRule> ReadOtherVersions(string plantCode, string prodNo, string prodRev);
    }
}
