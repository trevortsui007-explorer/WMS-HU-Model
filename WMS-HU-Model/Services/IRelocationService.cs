using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Relocation;

namespace WMS_HU_Model.Services
{
    public interface IRelocationService
    {
        OperationResult<BatchRelocationResultDto> MoveBatch(BatchRelocationRequest request);
    }
}
