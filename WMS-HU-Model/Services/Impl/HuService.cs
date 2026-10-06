using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class HuService : IHuService
    {
        private readonly IHuRepository _repository;
        private readonly IWeekCodeResolver _weekCodeResolver;

        public HuService(IHuRepository repository)
            : this(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()))
        {
        }

        public HuService(IHuRepository repository, IWeekCodeResolver weekCodeResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _weekCodeResolver = weekCodeResolver ?? throw new ArgumentNullException(nameof(weekCodeResolver));
        }

        public OperationResult<HuDto> Get(string huCode)
        {
            if (string.IsNullOrWhiteSpace(huCode))
                return OperationResult<HuDto>.Fail("HUCode不能为空。");

            huCode = huCode
                .Trim()
                .ToUpperInvariant();

            HuDto entity = _repository.GetByCode(huCode);

            if (entity == null)
                return OperationResult<HuDto>.Fail("HU不存在。");

            return OperationResult<HuDto>.Success(entity);
        }

        public OperationResult<HuDto> Create(CreateHuRequest request)
        {
            if (request == null)
                return OperationResult<HuDto>.Fail("请求不能为空。");

            if (string.IsNullOrWhiteSpace(request.HuCode))
                return OperationResult<HuDto>.Fail("HuCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.HuType))
                return OperationResult<HuDto>.Fail("HuType不能为空。");

            if (string.IsNullOrWhiteSpace(request.PlantCode))
                return OperationResult<HuDto>.Fail("PlantCode不能为空。");

            request.HuCode = request.HuCode
                .Trim()
                .ToUpperInvariant();

            request.HuType = request.HuType
                .Trim()
                .ToUpperInvariant();

            request.PlantCode = request.PlantCode
                .Trim()
                .ToUpperInvariant();

            if (!IsSupportedHuType(request.HuType))
                return OperationResult<HuDto>.Fail("HU类型只允许 PALLET、OUTER_BOX、INNER_PACK。");

            HuDto exists = _repository.GetByCode(request.HuCode);

            if (exists != null)
                return OperationResult<HuDto>.Fail("HU编码已存在。");

            long? packageSpecId = null;

            if (!string.IsNullOrWhiteSpace(request.PackageSpecCode))
            {
                packageSpecId = _repository.GetPackageSpecId(request.PackageSpecCode);

                if (!packageSpecId.HasValue)
                    return OperationResult<HuDto>.Fail("箱型不存在。");
            }

            string operationNo = Guid.NewGuid()
                .ToString("N")
                .ToUpperInvariant();

            _repository.Create(
                request,
                packageSpecId,
                operationNo);

            HuDto entity = _repository.GetByCode(request.HuCode);

            return OperationResult<HuDto>.Success(entity, "HU创建成功");
        }

        public OperationResult<bool> Attach(AttachHuRequest request)
        {
            if (request == null)
                return OperationResult<bool>.Fail("请求不能为空。");

            string parentCode = request.ParentHuCode == null
                ? null
                : request.ParentHuCode
                    .Trim()
                    .ToUpperInvariant();

            string childCode = request.ChildHuCode == null
                ? null
                : request.ChildHuCode
                    .Trim()
                    .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(parentCode))
                return OperationResult<bool>.Fail("ParentHuCode不能为空。");

            if (string.IsNullOrWhiteSpace(childCode))
                return OperationResult<bool>.Fail("ChildHuCode不能为空。");

            if (parentCode == childCode)
                return OperationResult<bool>.Fail("HU不能包含自身。");

            HuDto parent = _repository.GetByCode(parentCode);
            HuDto child = _repository.GetByCode(childCode);

            if (parent == null)
                return OperationResult<bool>.Fail("父HU不存在。");

            if (child == null)
                return OperationResult<bool>.Fail("子HU不存在。");

            if (parent.LifecycleStatus != "ACTIVE" || child.LifecycleStatus != "ACTIVE")
                return OperationResult<bool>.Fail("只有ACTIVE状态HU允许建立关系。");

            if (!IsRelationAllowed(parent.HuType, child.HuType))
            {
                return OperationResult<bool>.Fail(
                    "不允许的HU包装层级关系：" +
                    parent.HuType +
                    " → " +
                    child.HuType);
            }

            if (parent.PlantCode != child.PlantCode)
                return OperationResult<bool>.Fail("不同厂别HU暂不允许建立包装关系。");

            if (_repository.HasActiveParent(child.Id))
                return OperationResult<bool>.Fail("子HU已经存在有效父HU，请先执行拆出操作。");

            if (_repository.WouldCreateCycle(parent.Id, child.Id))
                return OperationResult<bool>.Fail("该操作会形成HU循环关系。");

            string operationNo = Guid.NewGuid()
                .ToString("N")
                .ToUpperInvariant();

            _repository.Attach(
                parent.Id,
                child.Id,
                request,
                operationNo);

            return OperationResult<bool>.Success(true, "HU关系建立成功");
        }

        public OperationResult<HuTreeNodeDto> GetTree(string huCode)
        {
            if (string.IsNullOrWhiteSpace(huCode))
                return OperationResult<HuTreeNodeDto>.Fail("HUCode不能为空。");

            HuDto root = _repository.GetByCode(
                huCode.Trim()
                    .ToUpperInvariant());

            if (root == null)
                return OperationResult<HuTreeNodeDto>.Fail("HU不存在。");

            List<HuTreeFlatRow> rows = _repository.GetTree(root.Id);

            Dictionary<long, HuTreeNodeDto> nodes = rows.ToDictionary(
                x => x.HuId,
                x => new HuTreeNodeDto
                {
                    Id = x.HuId,
                    HuCode = x.HuCode,
                    HuType = x.HuType,
                    PlantCode = x.PlantCode,
                    LifecycleStatus = x.LifecycleStatus,
                    IsHold = x.IsHold,
                    Items = _repository.GetItems(x.HuId),
                    Children = new List<HuTreeNodeDto>()
                });

            foreach (HuTreeFlatRow row in rows)
            {
                if (!row.ParentHuId.HasValue)
                    continue;

                nodes[row.ParentHuId.Value]
                    .Children
                    .Add(nodes[row.HuId]);
            }

            return OperationResult<HuTreeNodeDto>.Success(nodes[root.Id]);
        }

        public OperationResult<HuItemDto> AddItem(string huCode, AddHuItemRequest request)
        {
            if (request == null)
                return OperationResult<HuItemDto>.Fail("请求不能为空。");

            if (string.IsNullOrWhiteSpace(huCode))
                return OperationResult<HuItemDto>.Fail("HUCode不能为空。");

            HuDto hu = _repository.GetByCode(
                huCode.Trim()
                    .ToUpperInvariant());

            if (hu == null)
                return OperationResult<HuItemDto>.Fail("HU不存在。");

            if (hu.HuType != "INNER_PACK")
                return OperationResult<HuItemDto>.Fail("当前规则下库存明细只允许直接挂在INNER_PACK。");

            if (string.IsNullOrWhiteSpace(request.MaterialCode))
                return OperationResult<HuItemDto>.Fail("MaterialCode不能为空。");

            if (request.Qty <= 0)
                return OperationResult<HuItemDto>.Fail("Qty必须大于0。");

            if (string.IsNullOrWhiteSpace(request.Uom))
                request.Uom = "PCS";

            if (string.IsNullOrWhiteSpace(request.StockStatus))
                request.StockStatus = "AVAILABLE";

            request.MaterialCode = request.MaterialCode.Trim().ToUpperInvariant();
            request.MaterialRevision = string.IsNullOrWhiteSpace(request.MaterialRevision)
                ? null
                : request.MaterialRevision.Trim().ToUpperInvariant();
            request.DateCode = string.IsNullOrWhiteSpace(request.DateCode) ? null : request.DateCode.Trim();

            OperationResult<WeekCodeSnapshot> weekCodeResult = _weekCodeResolver.Resolve(
                request.MaterialCode,
                request.MaterialRevision,
                request.DateCode);

            if (!weekCodeResult.IsSuccess)
                return OperationResult<HuItemDto>.Fail(weekCodeResult.Message);

            string operationNo = Guid.NewGuid()
                .ToString("N")
                .ToUpperInvariant();

            long itemId = _repository.AddItem(
                hu.Id,
                request,
                weekCodeResult.Data,
                operationNo);

            HuItemDto item = _repository
                .GetItems(hu.Id)
                .FirstOrDefault(x => x.Id == itemId);

            return OperationResult<HuItemDto>.Success(item, "HU库存明细新增成功");
        }

        private static bool IsSupportedHuType(string huType)
        {
            return huType == "PALLET" ||
                   huType == "OUTER_BOX" ||
                   huType == "INNER_PACK";
        }

        private static bool IsRelationAllowed(string parentType, string childType)
        {
            if (parentType == "PALLET" && childType == "OUTER_BOX")
                return true;

            if (parentType == "OUTER_BOX" && childType == "INNER_PACK")
                return true;

            return false;
        }
    }
}
