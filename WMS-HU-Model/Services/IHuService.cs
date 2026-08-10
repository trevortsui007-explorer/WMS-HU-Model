using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Services
{
    public interface IHuService
    {
        OperationResult<HuDto> Get(
            string huCode);

        OperationResult<HuDto> Create(
            CreateHuRequest request);

        OperationResult<bool> Attach(
            AttachHuRequest request);

        OperationResult<HuItemDto> AddItem(
            string huCode,
            AddHuItemRequest request);

        OperationResult<HuTreeNodeDto> GetTree(
            string huCode);
    }
}