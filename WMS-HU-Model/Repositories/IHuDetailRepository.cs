using System.Collections.Generic;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public interface IHuDetailRepository
    {
        List<HuDetailContentDto> GetContents(long huId, string huType);
        HuHoldLogDto GetActiveHold(long huId);
        List<HuOperationLogDto> GetOperationLogs(long huId, int top);
        List<HuHoldLogDto> GetHoldLogs(long huId, int top);
    }
}
