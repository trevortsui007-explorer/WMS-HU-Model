using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.Inventory;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class InventoryQueryService : IInventoryQueryService
    {
        private readonly IInventoryQueryRepository _repository;
        private readonly IHuIdentifierService _identifierService;
        private readonly IEffectiveLocationResolver _effectiveLocationResolver;

        public InventoryQueryService(
            IInventoryQueryRepository repository,
            IHuIdentifierService identifierService,
            IEffectiveLocationResolver effectiveLocationResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _identifierService = identifierService ?? throw new ArgumentNullException(nameof(identifierService));
            _effectiveLocationResolver = effectiveLocationResolver ?? throw new ArgumentNullException(nameof(effectiveLocationResolver));
        }

        public OperationResult<InventoryLookupDto> Lookup(string warehouseCode, string code)
        {
            warehouseCode = Trim(warehouseCode);
            code = Trim(code);
            if (code == null) return OperationResult<InventoryLookupDto>.Fail("code不能为空。");
            if (warehouseCode != null && warehouseCode.Length > 30) return OperationResult<InventoryLookupDto>.Fail("warehouseCode长度不能超过30。");
            if (code.Length > 200) return OperationResult<InventoryLookupDto>.Fail("code长度不能超过200。");

            InventoryLookupLocationRecord location = warehouseCode != null && code.Length <= 50 ? _repository.GetLocation(warehouseCode, code) : null;
            if (location != null) return BuildLocation(code, location);

            OperationResult<HuIdentityDto> identity = _identifierService.Resolve(code, null);
            if (!identity.IsSuccess)
            {
                if (warehouseCode == null) return OperationResult<InventoryLookupDto>.Fail("请先扫描有效外箱，按厂别确定仓库。");
                if (identity.Message != null && identity.Message.IndexOf("多个HU", StringComparison.OrdinalIgnoreCase) >= 0)
                    return OperationResult<InventoryLookupDto>.Fail(identity.Message.Replace("箱码", "编码"));

                InventoryLookupLocationRecord foreignLocation = code.Length <= 50 ? _repository.FindLocation(code) : null;
                if (foreignLocation != null)
                    return OperationResult<InventoryLookupDto>.Fail("货架[" + code + "]不属于仓库[" + warehouseCode + "]。");

                return OperationResult<InventoryLookupDto>.Fail("未找到货架、外箱或内包：" + code);
            }

            HuDto hu = _repository.GetHu(identity.Data.HuId);
            if (hu == null) return OperationResult<InventoryLookupDto>.Fail("未找到货架、外箱或内包：" + code);
            if (!string.Equals(hu.HuType, "OUTER_BOX", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(hu.HuType, "INNER_PACK", StringComparison.OrdinalIgnoreCase))
                return OperationResult<InventoryLookupDto>.Fail("当前HU类型[" + hu.HuType + "]不支持库存查询。");

            HuLocationDto huLocation = _effectiveLocationResolver.Resolve(hu.Id);
            if (warehouseCode == null)
            {
                var warehouses = _repository.GetMappedWarehouses(hu.PlantCode);
                if (warehouses.Count != 1)
                    return OperationResult<InventoryLookupDto>.Fail("厂别[" + hu.PlantCode + "]需配置唯一启用的成品仓映射，当前匹配[" + string.Join(",", warehouses) + "]。");
                warehouseCode = warehouses[0];
            }
            string locationError = ValidateWarehouse(warehouseCode, huLocation);
            if (locationError != null) return OperationResult<InventoryLookupDto>.Fail(locationError);

            return string.Equals(hu.HuType, "OUTER_BOX", StringComparison.OrdinalIgnoreCase)
                ? BuildOuterBox(code, warehouseCode, hu, huLocation)
                : BuildInnerPack(code, warehouseCode, hu, huLocation);
        }

        private OperationResult<InventoryLookupDto> BuildLocation(string queryCode, InventoryLookupLocationRecord location)
        {
            InventoryLocationLookupDto result = new InventoryLocationLookupDto
            {
                QueryCode = queryCode,
                ResultType = "LOCATION",
                WarehouseCode = location.WarehouseCode,
                ZoneCode = location.ZoneCode,
                LocationCode = location.LocationCode
            };
            if (string.Equals(location.LocationCode, "SHIPPED", StringComparison.OrdinalIgnoreCase))
                return OperationResult<InventoryLookupDto>.Success(result);

            List<long> boxIds = _effectiveLocationResolver.FindHuIdsAtLocation(location.Id, "OUTER_BOX");
            List<InventoryItemFact> facts = _repository.GetLocationInventoryFacts(boxIds);

            foreach (IGrouping<long, InventoryItemFact> box in facts.GroupBy(x => x.BoxHuId).OrderBy(x => x.First().BoxCode, StringComparer.Ordinal))
            {
                List<InventoryItemFact> boxFacts = box.ToList();
                List<string> materialDimensions = boxFacts
                    .Select(x => Text(x.MaterialCode) + "\u001f" + Text(x.MaterialRevision))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (materialDimensions.Count > 1)
                    return OperationResult<InventoryLookupDto>.Fail("外箱[" + boxFacts[0].BoxCode + "]存在不一致的物料或版本，无法生成库存查询摘要。");

                result.Items.Add(new InventoryLocationItemDto
                {
                    BoxCode = boxFacts[0].BoxCode,
                    MaterialCode = Text(boxFacts[0].MaterialCode),
                    MaterialRevision = Text(boxFacts[0].MaterialRevision),
                    WeekDisplay = WeekDisplay(boxFacts),
                    Qty = boxFacts.Sum(x => x.Qty),
                    PackCount = boxFacts.Select(x => x.PackHuId).Distinct().Count(),
                    Status = boxFacts[0].BoxStatus,
                    IsHold = boxFacts[0].BoxIsHold
                });
            }

            return OperationResult<InventoryLookupDto>.Success(result);
        }

        private OperationResult<InventoryLookupDto> BuildOuterBox(string queryCode, string warehouseCode, HuDto hu, HuLocationDto location)
        {
            List<InventoryItemFact> facts = _repository.GetOuterBoxFacts(hu.Id);
            List<string> materialDimensions = facts
                .Select(x => Text(x.MaterialCode) + "\u001f" + Text(x.MaterialRevision))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (materialDimensions.Count > 1)
                return OperationResult<InventoryLookupDto>.Fail("外箱[" + hu.HuCode + "]存在不一致的物料或版本，无法生成库存查询摘要。");

            InventoryOuterBoxLookupDto result = new InventoryOuterBoxLookupDto
            {
                QueryCode = queryCode,
                ResultType = "OUTER_BOX",
                WarehouseCode = warehouseCode,
                BoxCode = hu.HuCode,
                HuStatus = hu.LifecycleStatus,
                IsHold = hu.IsHold,
                MaterialCode = facts.Count == 0 ? string.Empty : Text(facts[0].MaterialCode),
                MaterialRevision = facts.Count == 0 ? string.Empty : Text(facts[0].MaterialRevision),
                ZoneCode = string.Empty,
                LocationCode = string.Empty,
                LocationSource = string.Empty
            };
            ApplyLocation(result, location);

            result.Items = facts
                .GroupBy(x => new { PackCode = Text(x.PackCode), BatchNo = Text(x.BatchNo), WeekCode = WeekCode(x) })
                .OrderBy(x => x.Key.PackCode, StringComparer.Ordinal)
                .ThenBy(x => x.Key.BatchNo, StringComparer.Ordinal)
                .ThenBy(x => x.Key.WeekCode, StringComparer.Ordinal)
                .Select(x => new InventoryPackItemDto
                {
                    PackCode = x.Key.PackCode,
                    BatchNo = x.Key.BatchNo,
                    WeekCode = x.Key.WeekCode,
                    Qty = x.Sum(item => item.Qty)
                })
                .ToList();

            return OperationResult<InventoryLookupDto>.Success(result);
        }

        private OperationResult<InventoryLookupDto> BuildInnerPack(string queryCode, string warehouseCode, HuDto hu, HuLocationDto location)
        {
            List<InventoryItemFact> facts = _repository.GetInnerPackFacts(hu.Id);
            List<string> dimensions = facts
                .Select(x => Text(x.MaterialCode) + "\u001f" + Text(x.MaterialRevision) + "\u001f" + Text(x.BatchNo) + "\u001f" + WeekCode(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (dimensions.Count > 1)
                return OperationResult<InventoryLookupDto>.Fail(
                    "INNER_PACK_ITEM_CONFLICT: 内包[" + hu.HuCode + "]存在不一致的物料、版本、批次或周码，无法生成库存查询摘要。");

            InventoryInnerPackLookupDto result = new InventoryInnerPackLookupDto
            {
                QueryCode = queryCode,
                ResultType = "INNER_PACK",
                WarehouseCode = warehouseCode,
                PackCode = hu.HuCode,
                ParentBoxCode = Text(_repository.GetActiveParentBoxCode(hu.Id)),
                HuStatus = hu.LifecycleStatus,
                IsHold = hu.IsHold,
                MaterialCode = facts.Count == 0 ? string.Empty : Text(facts[0].MaterialCode),
                MaterialRevision = facts.Count == 0 ? string.Empty : Text(facts[0].MaterialRevision),
                BatchNo = facts.Count == 0 ? string.Empty : Text(facts[0].BatchNo),
                WeekCode = facts.Count == 0 ? string.Empty : WeekCode(facts[0]),
                TotalQty = facts.Sum(x => x.Qty),
                ZoneCode = string.Empty,
                LocationCode = string.Empty,
                LocationSource = string.Empty
            };
            ApplyLocation(result, location);
            return OperationResult<InventoryLookupDto>.Success(result);
        }

        private static string ValidateWarehouse(string warehouseCode, HuLocationDto location)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.EffectiveWarehouseCode)) return null;
            return string.Equals(location.EffectiveWarehouseCode, warehouseCode, StringComparison.OrdinalIgnoreCase)
                ? null
                : "货架[" + Text(location.EffectiveLocationCode) + "]不属于仓库[" + warehouseCode + "]。";
        }

        private static void ApplyLocation(InventoryOuterBoxLookupDto result, HuLocationDto location)
        {
            if (!IsPhysicalLocation(location)) return;
            result.ZoneCode = Text(location.EffectiveZoneCode);
            result.LocationCode = Text(location.EffectiveLocationCode);
            result.LocationSource = Text(location.LocationSource);
        }

        private static void ApplyLocation(InventoryInnerPackLookupDto result, HuLocationDto location)
        {
            if (!IsPhysicalLocation(location)) return;
            result.ZoneCode = Text(location.EffectiveZoneCode);
            result.LocationCode = Text(location.EffectiveLocationCode);
            result.LocationSource = Text(location.LocationSource);
        }

        private static bool IsPhysicalLocation(HuLocationDto location)
        {
            return location != null &&
                !string.IsNullOrWhiteSpace(location.EffectiveLocationCode) &&
                !string.Equals(location.EffectiveLocationCode, "SHIPPED", StringComparison.OrdinalIgnoreCase);
        }

        private static string WeekDisplay(IEnumerable<InventoryItemFact> facts)
        {
            return string.Join("/", facts
                .Where(x => x.WeekYear.HasValue && x.WeekNo.HasValue)
                .Select(x => new { Code = WeekCode(x), SortKey = x.WeekSortKey ?? (x.WeekYear.Value * 100 + x.WeekNo.Value) })
                .GroupBy(x => x.Code, StringComparer.Ordinal)
                .Select(x => x.OrderBy(value => value.SortKey).First())
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .Select(x => x.Code));
        }

        private static string WeekCode(InventoryItemFact fact)
        {
            if (!fact.WeekYear.HasValue || !fact.WeekNo.HasValue) return string.Empty;
            return (fact.WeekYear.Value % 100).ToString("00", CultureInfo.InvariantCulture) +
                fact.WeekNo.Value.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Text(string value)
        {
            return value ?? string.Empty;
        }
    }
}
