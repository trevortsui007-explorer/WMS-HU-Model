using WMS_HU_Model.Models.Relocation;

namespace WMS_HU_Model.Repositories
{
    public interface IRelocationRepository
    {
        BatchRelocationResultDto GetByRequestId(string requestId);
        BatchRelocationPersistenceResult ExecuteBatch(BatchRelocationCommand command);
    }
}
