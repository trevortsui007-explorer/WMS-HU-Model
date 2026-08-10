using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Services
{
    public interface IHuLocationService
    {
        OperationResult<HuLocationDto> GetLocation(
            string huCode);

        OperationResult<HuMoveResultDto> Move(
            string huCode,
            HuMoveRequest request);

        OperationResult<List<HuMoveLogDto>> GetMoveLogs(
            string huCode,
            int top);
    }
}