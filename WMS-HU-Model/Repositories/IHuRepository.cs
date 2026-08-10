using System.Collections.Generic;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public interface IHuRepository
    {
        HuDto GetByCode(string huCode);

        long? GetHuId(string huCode);

        string GetHuType(long huId);

        string GetPlantCode(long huId);

        long? GetPackageSpecId(
            string packageSpecCode);

        long Create(
            CreateHuRequest request,
            long? packageSpecId,
            string operationNo);

        bool HasActiveParent(
            long childHuId);

        bool WouldCreateCycle(
            long parentHuId,
            long childHuId);

        void Attach(
            long parentHuId,
            long childHuId,
            AttachHuRequest request,
            string operationNo);

        long AddItem(
            long huId,
            AddHuItemRequest request,
            string operationNo);

        List<HuTreeFlatRow> GetTree(
            long rootHuId);

        List<HuItemDto> GetItems(
            long huId);
    }
}