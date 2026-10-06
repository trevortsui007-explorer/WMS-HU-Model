using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Services
{
    public interface IHuDetailService
    {
        OperationResult<HuDetailDto> GetDetail(string huCode);
        OperationResult<List<HuOperationLogDto>> GetOperationLogs(string huCode, int top);
        OperationResult<List<HuHoldLogDto>> GetHoldLogs(string huCode, int top);
    }
}
