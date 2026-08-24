using System;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inventory;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class FinishedGoodsInventoryService : IFinishedGoodsInventoryService
    {
        private readonly IFinishedGoodsInventoryRepository _repository;

        public FinishedGoodsInventoryService(IFinishedGoodsInventoryRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public OperationResult<FinishedGoodsInventoryResultDto> Query(FinishedGoodsInventoryQuery query)
        {
            string error = NormalizeAndValidate(query);
            if (error != null)
                return OperationResult<FinishedGoodsInventoryResultDto>.Fail(error);

            return OperationResult<FinishedGoodsInventoryResultDto>.Success(_repository.Query(query));
        }

        public OperationResult<FinishedGoodsInventoryPreviewDto> GetPreview(string boxCode)
        {
            boxCode = Trim(boxCode);
            if (boxCode == null)
                return OperationResult<FinishedGoodsInventoryPreviewDto>.Fail("箱码不能为空。");
            if (boxCode.Length > 100)
                return OperationResult<FinishedGoodsInventoryPreviewDto>.Fail("箱码长度不能超过100。");

            FinishedGoodsInventoryPreviewDto preview = _repository.GetPreview(boxCode);
            return preview == null
                ? OperationResult<FinishedGoodsInventoryPreviewDto>.Fail("未找到当前成品库存箱。")
                : OperationResult<FinishedGoodsInventoryPreviewDto>.Success(preview);
        }

        private static string NormalizeAndValidate(FinishedGoodsInventoryQuery query)
        {
            if (query == null)
                return "查询参数不能为空。";

            query.PlantCode = Trim(query.PlantCode);
            query.MaterialCode = Trim(query.MaterialCode);
            query.MaterialRevision = Trim(query.MaterialRevision);
            query.BoxCode = Trim(query.BoxCode);
            query.InnerPackCode = Trim(query.InnerPackCode);
            query.LocationCode = Trim(query.LocationCode);
            query.WeekCode = Trim(query.WeekCode);
            query.StockStatus = Trim(query.StockStatus);
            if (query.Page == 0) query.Page = 1;
            if (query.PageSize == 0) query.PageSize = 50;

            if (query.Page < 1) return "page必须大于等于1。";
            if (query.PageSize < 1 || query.PageSize > 200) return "pageSize必须在1到200之间。";
            if (TooLong(query.PlantCode, 20)) return "厂别长度不能超过20。";
            if (TooLong(query.MaterialCode, 50)) return "物料长度不能超过50。";
            if (TooLong(query.MaterialRevision, 30)) return "版本长度不能超过30。";
            if (TooLong(query.BoxCode, 100) || TooLong(query.InnerPackCode, 100)) return "HU编码长度不能超过100。";
            if (TooLong(query.LocationCode, 50)) return "货位长度不能超过50。";
            if (TooLong(query.WeekCode, 20)) return "周码长度不能超过20。";
            if (TooLong(query.StockStatus, 20)) return "库存状态长度不能超过20。";
            return null;
        }

        private static bool TooLong(string value, int maxLength)
        {
            return value != null && value.Length > maxLength;
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
