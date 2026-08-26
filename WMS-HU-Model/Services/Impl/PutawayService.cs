using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Models.Putaway;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class PutawayService : IPutawayService
    {
        private readonly IHuRepository _huRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IHuLocationRepository _huLocationRepository;
        private readonly IPutawayRepository _putawayRepository;
        private readonly IInventoryQueryRepository _inventoryRepository;
        private readonly IEffectiveLocationResolver _effectiveLocationResolver;

        public PutawayService(
            IHuRepository huRepository,
            ILocationRepository locationRepository,
            IHuLocationRepository huLocationRepository,
            IPutawayRepository putawayRepository)
            : this(huRepository, locationRepository, huLocationRepository, putawayRepository, new InventoryQueryRepository(), new EffectiveLocationResolver())
        {
        }

        public PutawayService(
            IHuRepository huRepository,
            ILocationRepository locationRepository,
            IHuLocationRepository huLocationRepository,
            IPutawayRepository putawayRepository,
            IInventoryQueryRepository inventoryRepository,
            IEffectiveLocationResolver effectiveLocationResolver)
        {
            _huRepository = huRepository;
            _locationRepository = locationRepository;
            _huLocationRepository = huLocationRepository;
            _putawayRepository = putawayRepository;
            _inventoryRepository = inventoryRepository;
            _effectiveLocationResolver = effectiveLocationResolver;
        }

        public OperationResult<PalletPutawayStatusDto> GetPalletStatus(string palletHuCode)
        {
            if (string.IsNullOrWhiteSpace(palletHuCode))
            {
                return OperationResult<PalletPutawayStatusDto>.Fail("PalletHuCode不能为空。");
            }

            string normalizedCode = palletHuCode.Trim().ToUpperInvariant();

            HuDto pallet = _huRepository.GetByCode(normalizedCode);

            if (pallet == null)
            {
                return OperationResult<PalletPutawayStatusDto>.Fail("托盘HU不存在。");
            }

            if (pallet.HuType != "PALLET")
            {
                return OperationResult<PalletPutawayStatusDto>.Fail("当前HU不是PALLET。");
            }

            PalletPutawayStatusDto status = _putawayRepository.GetPalletStatus(pallet.Id);

            return OperationResult<PalletPutawayStatusDto>.Success(status);
        }

        public OperationResult<PutawayResultDto> PutawayBox(PutawayBoxRequest request)
        {
            if (request == null)
            {
                return OperationResult<PutawayResultDto>.Fail("请求不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.BoxHuCode))
            {
                return OperationResult<PutawayResultDto>.Fail("BoxHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.WarehouseCode))
            {
                return OperationResult<PutawayResultDto>.Fail("WarehouseCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.ToLocationCode))
            {
                return OperationResult<PutawayResultDto>.Fail("ToLocationCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                return OperationResult<PutawayResultDto>.Fail("RequestId不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.Operator))
            {
                return OperationResult<PutawayResultDto>.Fail("Operator不能为空。");
            }

            string boxCode = request.BoxHuCode.Trim().ToUpperInvariant();
            string warehouseCode = request.WarehouseCode.Trim().ToUpperInvariant();
            string targetCode = request.ToLocationCode.Trim().ToUpperInvariant();

            //----------------------------------------------------
            // 1. 获取外箱
            //----------------------------------------------------
            HuDto box = _huRepository.GetByCode(boxCode);

            if (box == null)
            {
                return OperationResult<PutawayResultDto>.Fail("外箱HU不存在。");
            }

            if (box.HuType != "OUTER_BOX")
            {
                return OperationResult<PutawayResultDto>.Fail("当前HU不是OUTER_BOX。");
            }

            //----------------------------------------------------
            // 2. 幂等
            //----------------------------------------------------
            PutawayResultDto duplicate = _putawayRepository.GetByRequestId(request.RequestId);

            if (duplicate != null)
            {
                if (!string.Equals(duplicate.BoxHuCode, boxCode, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<PutawayResultDto>.Fail("RequestId已经被其他上架业务使用。");
                }

                if (!string.Equals(duplicate.ToLocationCode, targetCode, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<PutawayResultDto>.Fail("RequestId对应的目标货位与当前请求不一致。");
                }

                duplicate.IsDuplicate = true;

                return OperationResult<PutawayResultDto>.Success(duplicate, "重复请求，返回原上架结果");
            }

            //----------------------------------------------------
            // 3. 防止RequestId被其他Move业务占用
            //----------------------------------------------------
            HuMoveLogDto existingMove = _huLocationRepository.GetMoveByRequestId(request.RequestId);

            if (existingMove != null)
            {
                return OperationResult<PutawayResultDto>.Fail("RequestId已经被其他移动业务使用。");
            }

            //----------------------------------------------------
            // 4. 根据Box自动反查当前运输托
            //----------------------------------------------------
            HuDto pallet = _huRepository.GetActiveParent(box.Id);

            if (pallet == null)
            {
                return OperationResult<PutawayResultDto>.Fail("外箱当前没有有效运输托关系。");
            }

            if (pallet.HuType != "PALLET")
            {
                return OperationResult<PutawayResultDto>.Fail("外箱当前父HU不是PALLET。");
            }

            //----------------------------------------------------
            // 5. HU状态校验
            //----------------------------------------------------
            if (pallet.LifecycleStatus != "ACTIVE" || box.LifecycleStatus != "ACTIVE")
            {
                return OperationResult<PutawayResultDto>.Fail("只有ACTIVE状态HU允许上架。");
            }

            if (pallet.IsHold || box.IsHold)
            {
                return OperationResult<PutawayResultDto>.Fail("托盘或外箱当前已冻结。");
            }

            if (!string.Equals(pallet.PlantCode, box.PlantCode, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<PutawayResultDto>.Fail("托盘与外箱厂别不一致。");
            }

            //----------------------------------------------------
            // 6. 确认Box仍属于当前运输托
            //----------------------------------------------------
            PalletPutawayStatusDto palletStatus = _putawayRepository.GetPalletStatus(pallet.Id);

            if (palletStatus == null)
            {
                return OperationResult<PutawayResultDto>.Fail("运输托状态不存在。");
            }

            bool boxInPallet = palletStatus.RemainingBoxCodes
                .Any(x => string.Equals(x, boxCode, StringComparison.OrdinalIgnoreCase));

            if (!boxInPallet)
            {
                return OperationResult<PutawayResultDto>.Fail("该外箱当前不属于指定托盘。");
            }

            //----------------------------------------------------
            // 7. 托盘必须位于收货区域
            //----------------------------------------------------
            HuLocationDto palletLocation = _huLocationRepository.GetLocation(pallet.Id);

            if (palletLocation == null || !palletLocation.DirectLocationId.HasValue)
            {
                return OperationResult<PutawayResultDto>.Fail("托盘没有直接当前位置。");
            }

            if (palletLocation.DirectZoneCode != "RECEIVE")
            {
                return OperationResult<PutawayResultDto>.Fail("托盘当前不在收货区域。");
            }

            //----------------------------------------------------
            // 8. 外箱应继承托盘位置，不能已有DirectLocation
            //----------------------------------------------------
            HuLocationDto boxLocation = _huLocationRepository.GetLocation(box.Id);

            if (boxLocation == null)
            {
                return OperationResult<PutawayResultDto>.Fail("无法获取外箱有效位置。");
            }

            if (boxLocation.DirectLocationId.HasValue)
            {
                return OperationResult<PutawayResultDto>.Fail("外箱已经存在直接货位。");
            }

            if (!boxLocation.LocationSourceHuId.HasValue ||
                boxLocation.LocationSourceHuId.Value != pallet.Id)
            {
                return OperationResult<PutawayResultDto>.Fail("外箱当前有效位置不是从指定托盘继承。");
            }

            //----------------------------------------------------
            // 9. 目标货位
            //----------------------------------------------------
            LocationDto target = _locationRepository.GetByCode(warehouseCode, targetCode);

            if (target == null)
            {
                return OperationResult<PutawayResultDto>.Fail("目标货位不存在。");
            }

            if (target.MasterStatus != "ACTIVE")
            {
                return OperationResult<PutawayResultDto>.Fail("目标货位当前不可用。");
            }

            if (target.IsVirtual)
            {
                return OperationResult<PutawayResultDto>.Fail("正常上架不能使用虚拟货位。");
            }

            if (target.ZoneCode != "STORAGE")
            {
                return OperationResult<PutawayResultDto>.Fail("正常上架目标必须属于STORAGE区域。");
            }

            if (!_huLocationRepository.WarehouseAllowsPlant(warehouseCode, box.PlantCode))
            {
                return OperationResult<PutawayResultDto>.Fail("仓库不允许存储该厂别库存。");
            }

            //----------------------------------------------------
            // 10. SAME_PLANT
            //----------------------------------------------------
            if (target.PlantPolicy == "SAME_PLANT")
            {
                int differentPlantCount = _huLocationRepository.CountDifferentPlantAtLocation(
                    target.Id,
                    box.PlantCode);

                if (differentPlantCount > 0)
                {
                    return OperationResult<PutawayResultDto>.Fail("目标货位当前存在其他厂别库存。");
                }
            }

            //----------------------------------------------------
            // 11. DEDICATED
            //----------------------------------------------------
            if (target.PlantPolicy == "DEDICATED" &&
                !string.Equals(target.DedicatedPlantCode, box.PlantCode, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<PutawayResultDto>.Fail("目标货位为其他厂别专用货位。");
            }

            //----------------------------------------------------
            // 12. 构建事务命令
            //----------------------------------------------------
            PutawayCommand command = new PutawayCommand
            {
                PalletHuId = pallet.Id,
                PalletHuCode = pallet.HuCode,
                BoxHuId = box.Id,
                BoxHuCode = box.HuCode,
                PlantCode = box.PlantCode,
                FromLocationId = palletLocation.DirectLocationId.Value,
                FromLocationCode = palletLocation.DirectLocationCode,
                ToLocationId = target.Id,
                ToLocationCode = target.LocationCode,
                WarehouseCode = warehouseCode,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                RequestId = request.RequestId,
                ScannedLocationCode = string.IsNullOrWhiteSpace(request.ScannedLocationCode)
                    ? target.LocationCode
                    : request.ScannedLocationCode,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };

            PutawayPersistenceResult persistResult = _putawayRepository.ExecutePutaway(command);

            if (!persistResult.IsSuccess)
            {
                return OperationResult<PutawayResultDto>.Fail(persistResult.Message);
            }

            return OperationResult<PutawayResultDto>.Success(persistResult.Data, "外箱拆托上架成功");
        }

        public OperationResult<PalletPutawayByBoxContextDto> GetPalletContextByBox(string boxHuCode)
        {
            if (string.IsNullOrWhiteSpace(boxHuCode)) return OperationResult<PalletPutawayByBoxContextDto>.Fail("BoxHuCode不能为空。");
            string normalized = boxHuCode.Trim().ToUpperInvariant();
            if (normalized.Length > 100) return OperationResult<PalletPutawayByBoxContextDto>.Fail("BoxHuCode长度不能超过100。");

            HuDto scannedBox = _huRepository.GetByCode(normalized);
            if (scannedBox == null || scannedBox.HuType != "OUTER_BOX") return OperationResult<PalletPutawayByBoxContextDto>.Fail("外箱不存在或HU类型错误。");
            HuDto pallet = _huRepository.GetActiveParent(scannedBox.Id);
            if (pallet == null || pallet.HuType != "PALLET") return OperationResult<PalletPutawayByBoxContextDto>.Fail("外箱当前没有有效运输托关系。");

            PalletPutawayStatusDto status = _putawayRepository.GetPalletStatus(pallet.Id);
            if (status == null) return OperationResult<PalletPutawayByBoxContextDto>.Fail("运输托状态不存在。");
            if (!status.RemainingBoxCodes.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
                return OperationResult<PalletPutawayByBoxContextDto>.Fail("该外箱当前不属于可上架运输托范围。");

            List<HuDto> boxes = status.RemainingBoxCodes.Select(_huRepository.GetByCode).Where(x => x != null).ToList();
            Dictionary<long, HuLocationDto> locations = _effectiveLocationResolver.ResolveMany(boxes.Select(x => x.Id).ToList()).ToDictionary(x => x.HuId);
            List<Models.Inventory.InventoryItemFact> palletFacts = _inventoryRepository.GetLocationInventoryFacts(boxes.Select(x => x.Id).ToList()) ?? new List<Models.Inventory.InventoryItemFact>();
            PalletPutawayByBoxContextDto result = new PalletPutawayByBoxContextDto
            {
                ScannedBoxCode = scannedBox.HuCode,
                PalletHuCode = pallet.HuCode
            };
            foreach (HuDto box in boxes.OrderBy(x => x.HuCode, StringComparer.OrdinalIgnoreCase))
            {
                List<Models.Inventory.InventoryItemFact> facts = palletFacts.Where(x => x.BoxHuId == box.Id).ToList();
                List<string> materials = facts.Select(x => x.MaterialCode ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                List<string> revisions = facts.Select(x => x.MaterialRevision ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (materials.Count > 1 || revisions.Count > 1)
                    return OperationResult<PalletPutawayByBoxContextDto>.Fail("外箱[" + box.HuCode + "]存在不一致的物料或版本，无法生成整托上架摘要。");

                HuLocationDto location;
                locations.TryGetValue(box.Id, out location);
                result.Boxes.Add(new PalletPutawayBoxDto
                {
                    BoxCode = box.HuCode,
                    MaterialCode = materials.FirstOrDefault(),
                    MaterialRevision = revisions.FirstOrDefault(),
                    Qty = facts.Sum(x => x.Qty),
                    WeekSummary = string.Join("/", facts.Where(x => x.WeekYear.HasValue && x.WeekNo.HasValue)
                        .Select(x => new { x.WeekYear, x.WeekNo, x.WeekSortKey }).Distinct()
                        .OrderBy(x => x.WeekSortKey).ThenBy(x => x.WeekYear).ThenBy(x => x.WeekNo)
                        .Select(x => (x.WeekYear.Value % 100).ToString("00") + x.WeekNo.Value.ToString("00"))),
                    HuStatus = box.LifecycleStatus,
                    IsHold = box.IsHold,
                    EffectiveLocationCode = location == null ? null : location.EffectiveLocationCode,
                    LocationSource = location == null ? string.Empty : location.LocationSource
                });
            }
            HuLocationDto palletLocation = _effectiveLocationResolver.Resolve(pallet.Id);
            result.WarehouseCode = palletLocation == null ? null : palletLocation.EffectiveWarehouseCode;
            result.EffectiveLocationCode = palletLocation == null ? null : palletLocation.EffectiveLocationCode;
            result.LocationSource = palletLocation == null ? string.Empty : palletLocation.LocationSource;
            result.BoxCount = result.Boxes.Count;
            result.TotalQty = result.Boxes.Sum(x => x.Qty);
            return OperationResult<PalletPutawayByBoxContextDto>.Success(result);
        }

        public OperationResult<WholePalletPutawayResultDto> PutawayPallet(WholePalletPutawayRequest request)
        {
            if (request == null) return OperationResult<WholePalletPutawayResultDto>.Fail("请求不能为空。");
            request.PalletHuCode = Normalize(request.PalletHuCode);
            request.WarehouseCode = Normalize(request.WarehouseCode);
            request.TargetLocationCode = Normalize(request.TargetLocationCode);
            request.ScannedLocationCode = Normalize(request.ScannedLocationCode);
            request.RequestId = Text(request.RequestId);
            request.Operator = Text(request.Operator);
            request.BusinessNo = Text(request.BusinessNo);
            request.Remark = Text(request.Remark);
            if (request.PalletHuCode == null) return OperationResult<WholePalletPutawayResultDto>.Fail("PalletHuCode不能为空。");
            if (request.WarehouseCode == null) return OperationResult<WholePalletPutawayResultDto>.Fail("WarehouseCode不能为空。");
            if (request.TargetLocationCode == null) return OperationResult<WholePalletPutawayResultDto>.Fail("TargetLocationCode不能为空。");
            if (request.RequestId == null) return OperationResult<WholePalletPutawayResultDto>.Fail("RequestId不能为空。");
            if (request.Operator == null) return OperationResult<WholePalletPutawayResultDto>.Fail("Operator不能为空。");
            if (request.PalletHuCode.Length > 100 || request.TargetLocationCode.Length > 50 || request.WarehouseCode.Length > 30 || request.RequestId.Length > 90)
                return OperationResult<WholePalletPutawayResultDto>.Fail("请求字段长度超限。");

            WholePalletPutawayResultDto duplicate = _putawayRepository.GetWholePalletPutawayByRequestId(request.RequestId);
            if (duplicate != null)
            {
                if (!string.Equals(duplicate.PalletHuCode, request.PalletHuCode, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(duplicate.ToLocationCode, request.TargetLocationCode, StringComparison.OrdinalIgnoreCase))
                    return OperationResult<WholePalletPutawayResultDto>.Fail("RequestId已经被其他整托上架业务使用。");
                duplicate.IsDuplicate = true;
                return OperationResult<WholePalletPutawayResultDto>.Success(duplicate, "重复请求，返回原整托上架结果");
            }

            WholePalletPutawayCommand command = new WholePalletPutawayCommand
            {
                PalletHuCode = request.PalletHuCode,
                WarehouseCode = request.WarehouseCode,
                TargetLocationCode = request.TargetLocationCode,
                ScannedLocationCode = request.ScannedLocationCode ?? request.TargetLocationCode,
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant()
            };
            WholePalletPutawayPersistenceResult persisted = _putawayRepository.ExecuteWholePalletPutaway(command);
            return persisted.IsSuccess
                ? OperationResult<WholePalletPutawayResultDto>.Success(persisted.Data, "整托上架成功")
                : OperationResult<WholePalletPutawayResultDto>.Fail(persisted.Message);
        }

        private static string Normalize(string value)
        {
            string text = Text(value);
            return text == null ? null : text.ToUpperInvariant();
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
