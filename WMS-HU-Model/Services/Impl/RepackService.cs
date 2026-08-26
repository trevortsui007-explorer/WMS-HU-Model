using System;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Models.Repack;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class RepackService : IRepackService
    {
        private readonly IHuRepository _huRepository;
        private readonly IHuLocationRepository _huLocationRepository;
        private readonly IRepackRepository _repackRepository;
        private readonly ILocationRepository _locationRepository;

        public RepackService(
            IHuRepository huRepository,
            ILocationRepository locationRepository,
            IHuLocationRepository huLocationRepository,
            IRepackRepository repackRepository)
        {
            _huRepository = huRepository;

            _locationRepository = locationRepository;

            _huLocationRepository = huLocationRepository;

            _repackRepository = repackRepository;
        }

        public OperationResult<BoxRepackStatusDto> GetBoxStatus(string boxHuCode)
        {
            if (string.IsNullOrWhiteSpace(boxHuCode))
                return OperationResult<BoxRepackStatusDto>.Fail("BoxHuCode不能为空。");

            string normalizedCode = boxHuCode
                .Trim()
                .ToUpperInvariant();

            HuDto box = _huRepository.GetByCode(normalizedCode);

            if (box == null)
                return OperationResult<BoxRepackStatusDto>.Fail("外箱HU不存在。");

            if (box.HuType != "OUTER_BOX")
                return OperationResult<BoxRepackStatusDto>.Fail("当前HU不是OUTER_BOX。");

            BoxRepackStatusDto result = _repackRepository.GetBoxStatus(box.Id);

            return OperationResult<BoxRepackStatusDto>.Success(result);
        }
        public OperationResult<TransferInnerResultDto> TransferInner(TransferInnerRequest request)
        {
            if (request == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("请求不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.SourceBoxHuCode))
            {
                return OperationResult<TransferInnerResultDto>.Fail("SourceBoxHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.TargetBoxHuCode))
            {
                return OperationResult<TransferInnerResultDto>.Fail("TargetBoxHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.InnerHuCode))
            {
                return OperationResult<TransferInnerResultDto>.Fail("InnerHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                return OperationResult<TransferInnerResultDto>.Fail("RequestId不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.Operator))
            {
                return OperationResult<TransferInnerResultDto>.Fail("Operator不能为空。");
            }

            string sourceCode = request.SourceBoxHuCode.Trim().ToUpperInvariant();
            string targetCode = request.TargetBoxHuCode.Trim().ToUpperInvariant();
            string innerCode = request.InnerHuCode.Trim().ToUpperInvariant();

            if (sourceCode == targetCode)
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱和目标外箱不能相同。");
            }

            //----------------------------------------------------
            // 幂等
            //----------------------------------------------------
            TransferInnerResultDto duplicate = _repackRepository.GetTransferByRequestId(request.RequestId);

            if (duplicate != null)
            {
                bool sameRequest =
                    string.Equals(duplicate.SourceBoxHuCode, sourceCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(duplicate.TargetBoxHuCode, targetCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(duplicate.InnerHuCode, innerCode, StringComparison.OrdinalIgnoreCase);

                if (!sameRequest)
                {
                    return OperationResult<TransferInnerResultDto>.Fail("RequestId已经被其他Repack业务使用。");
                }

                duplicate.IsDuplicate = true;

                return OperationResult<TransferInnerResultDto>.Success(duplicate, "重复请求，返回原Repack结果");
            }

            HuDto source = _huRepository.GetByCode(sourceCode);
            HuDto target = _huRepository.GetByCode(targetCode);
            HuDto inner = _huRepository.GetByCode(innerCode);

            if (source == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱不存在。");
            }

            if (target == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("目标外箱不存在。");
            }

            if (inner == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("内包HU不存在。");
            }

            if (source.HuType != "OUTER_BOX" || target.HuType != "OUTER_BOX")
            {
                return OperationResult<TransferInnerResultDto>.Fail("源HU和目标HU必须都是OUTER_BOX。");
            }

            if (inner.HuType != "INNER_PACK")
            {
                return OperationResult<TransferInnerResultDto>.Fail("转移对象必须是INNER_PACK。");
            }

            if (source.LifecycleStatus != "ACTIVE" ||
                target.LifecycleStatus != "ACTIVE" ||
                inner.LifecycleStatus != "ACTIVE")
            {
                return OperationResult<TransferInnerResultDto>.Fail("只有ACTIVE状态HU允许Repack。");
            }

            if (source.IsHold || target.IsHold || inner.IsHold)
            {
                return OperationResult<TransferInnerResultDto>.Fail("参与Repack的HU存在冻结状态。");
            }

            if (!string.Equals(source.PlantCode, target.PlantCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(source.PlantCode, inner.PlantCode, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱、目标外箱、内包厂别必须一致。");
            }

            //----------------------------------------------------
            // Source确认包含Inner
            //----------------------------------------------------
            BoxRepackStatusDto sourceStatus = _repackRepository.GetBoxStatus(source.Id);

            bool sourceContainsInner = sourceStatus.InnerPackCodes
                .Any(x => string.Equals(x, innerCode, StringComparison.OrdinalIgnoreCase));

            if (!sourceContainsInner)
            {
                return OperationResult<TransferInnerResultDto>.Fail("内包当前不属于指定源外箱。");
            }

            //----------------------------------------------------
            // 两个Box必须有直接位置
            //----------------------------------------------------
            HuLocationDto sourceLocation = _huLocationRepository.GetLocation(source.Id);
            HuLocationDto targetLocation = _huLocationRepository.GetLocation(target.Id);
            HuLocationDto innerLocation = _huLocationRepository.GetLocation(inner.Id);

            if (sourceLocation == null || !sourceLocation.DirectLocationId.HasValue)
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱没有直接货位。");
            }

            if (targetLocation == null || !targetLocation.DirectLocationId.HasValue)
            {
                return OperationResult<TransferInnerResultDto>.Fail("目标外箱没有直接货位。");
            }

            if (innerLocation != null && innerLocation.DirectLocationId.HasValue)
            {
                return OperationResult<TransferInnerResultDto>.Fail("当前内包已经有独立货位，不能执行箱到箱Repack。");
            }

            TransferInnerCommand command = new TransferInnerCommand
            {
                SourceBoxHuId = source.Id,
                SourceBoxHuCode = source.HuCode,
                TargetBoxHuId = target.Id,
                TargetBoxHuCode = target.HuCode,
                InnerHuId = inner.Id,
                InnerHuCode = inner.HuCode,
                PlantCode = inner.PlantCode,
                FromLocationId = sourceLocation.DirectLocationId.Value,
                FromLocationCode = sourceLocation.DirectLocationCode,
                ToLocationId = targetLocation.DirectLocationId.Value,
                ToLocationCode = targetLocation.DirectLocationCode,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };

            TransferInnerPersistenceResult persistResult = _repackRepository.ExecuteTransferInner(command);

            if (!persistResult.IsSuccess)
            {
                return OperationResult<TransferInnerResultDto>.Fail(persistResult.Message);
            }

            return OperationResult<TransferInnerResultDto>.Success(persistResult.Data, "内包换箱成功");
        }

        public OperationResult<UnpackInnerResultDto> UnpackInner(UnpackInnerRequest request)
        {
            // 1. 参数非空校验
            if (request == null)
                return OperationResult<UnpackInnerResultDto>.Fail("请求不能为空。");

            if (string.IsNullOrWhiteSpace(request.SourceBoxHuCode))
                return OperationResult<UnpackInnerResultDto>.Fail("SourceBoxHuCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.InnerHuCode))
                return OperationResult<UnpackInnerResultDto>.Fail("InnerHuCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.WarehouseCode))
                return OperationResult<UnpackInnerResultDto>.Fail("WarehouseCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.ToLocationCode))
                return OperationResult<UnpackInnerResultDto>.Fail("ToLocationCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.RequestId))
                return OperationResult<UnpackInnerResultDto>.Fail("RequestId不能为空。");

            if (string.IsNullOrWhiteSpace(request.Operator))
                return OperationResult<UnpackInnerResultDto>.Fail("Operator不能为空。");

            // 数据标准化
            string sourceCode = request.SourceBoxHuCode.Trim().ToUpperInvariant();
            string innerCode = request.InnerHuCode.Trim().ToUpperInvariant();
            string warehouseCode = request.WarehouseCode.Trim().ToUpperInvariant();
            string targetCode = request.ToLocationCode.Trim().ToUpperInvariant();

            // 2. 幂等性校验
            UnpackInnerResultDto duplicate = _repackRepository.GetUnpackByRequestId(request.RequestId);
            if (duplicate != null)
            {
                bool sameRequest = string.Equals(duplicate.SourceBoxHuCode, sourceCode, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(duplicate.InnerHuCode, innerCode, StringComparison.OrdinalIgnoreCase);

                if (!sameRequest)
                {
                    return OperationResult<UnpackInnerResultDto>.Fail("RequestId已经被其他拆箱业务使用。");
                }

                duplicate.IsDuplicate = true;
                return OperationResult<UnpackInnerResultDto>.Success(duplicate, "重复请求，返回原拆箱结果");
            }

            HuMoveLogDto existingMove = _huLocationRepository.GetMoveByRequestId(request.RequestId);
            if (existingMove != null)
            {
                return OperationResult<UnpackInnerResultDto>.Fail("RequestId已经被其他移动业务使用。");
            }

            // 3. 业务基础数据校验
            HuDto source = _huRepository.GetByCode(sourceCode);
            HuDto inner = _huRepository.GetByCode(innerCode);

            if (source == null || source.HuType != "OUTER_BOX")
                return OperationResult<UnpackInnerResultDto>.Fail("源外箱不存在或HU类型错误。");

            if (inner == null || inner.HuType != "INNER_PACK")
                return OperationResult<UnpackInnerResultDto>.Fail("内包不存在或HU类型错误。");

            if (source.LifecycleStatus != "ACTIVE" || inner.LifecycleStatus != "ACTIVE")
                return OperationResult<UnpackInnerResultDto>.Fail("只有ACTIVE状态HU允许拆箱。");

            if (source.IsHold || inner.IsHold)
                return OperationResult<UnpackInnerResultDto>.Fail("参与拆箱的HU已被冻结。");

            if (!string.Equals(source.PlantCode, inner.PlantCode, StringComparison.OrdinalIgnoreCase))
                return OperationResult<UnpackInnerResultDto>.Fail("外箱与内包厂别不一致。");

            // 4. 层级关系校验
            BoxRepackStatusDto sourceStatus = _repackRepository.GetBoxStatus(source.Id);
            bool containsInner = sourceStatus.InnerPackCodes.Any(x => string.Equals(x, innerCode, StringComparison.OrdinalIgnoreCase));
            if (!containsInner)
            {
                return OperationResult<UnpackInnerResultDto>.Fail("内包当前不属于指定外箱。");
            }

            // 5. 货位与库位策略校验
            HuLocationDto sourceLocation = _huLocationRepository.GetLocation(source.Id);
            HuLocationDto innerLocation = _huLocationRepository.GetLocation(inner.Id);

            if (sourceLocation == null || !sourceLocation.DirectLocationId.HasValue)
                return OperationResult<UnpackInnerResultDto>.Fail("源外箱没有直接货位。");

            if (innerLocation != null && innerLocation.DirectLocationId.HasValue)
                return OperationResult<UnpackInnerResultDto>.Fail("内包已经有直接货位。");

            LocationDto target = _locationRepository.GetByCode(warehouseCode, targetCode);
            if (target == null)
                return OperationResult<UnpackInnerResultDto>.Fail("目标货位不存在。");

            if (target.MasterStatus != "ACTIVE")
                return OperationResult<UnpackInnerResultDto>.Fail("目标货位不可用。");

            if (target.IsVirtual)
                return OperationResult<UnpackInnerResultDto>.Fail("拆出的内包必须进入实体货位。");

            if (target.ZoneCode != "STORAGE")
                return OperationResult<UnpackInnerResultDto>.Fail("目标货位必须属于STORAGE区域。");

            if (!_huLocationRepository.WarehouseAllowsPlant(warehouseCode, inner.PlantCode))
                return OperationResult<UnpackInnerResultDto>.Fail("仓库不允许存储该厂别库存。");

            if (target.PlantPolicy == "DEDICATED")
            {
                if (!string.Equals(target.DedicatedPlantCode, inner.PlantCode, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<UnpackInnerResultDto>.Fail("目标货位为其他厂别专用货位。");
                }
            }

            if (target.PlantPolicy == "SAME_PLANT")
            {
                int count = _huLocationRepository.CountDifferentPlantAtLocation(target.Id, inner.PlantCode);
                if (count > 0)
                {
                    return OperationResult<UnpackInnerResultDto>.Fail("目标货位存在其他厂别库存。");
                }
            }

            // 6. 构建 Command 并调用持久层事务
            UnpackInnerCommand command = new UnpackInnerCommand
            {
                SourceBoxHuId = source.Id,
                SourceBoxHuCode = source.HuCode,
                InnerHuId = inner.Id,
                InnerHuCode = inner.HuCode,
                PlantCode = inner.PlantCode,
                FromLocationId = sourceLocation.DirectLocationId.Value,
                FromLocationCode = sourceLocation.DirectLocationCode,
                ToLocationId = target.Id,
                ToLocationCode = target.LocationCode,
                WarehouseCode = warehouseCode,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                RequestId = request.RequestId,
                ScannedLocationCode = string.IsNullOrWhiteSpace(request.ScannedLocationCode) ? target.LocationCode : request.ScannedLocationCode,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };

            UnpackInnerPersistenceResult result = _repackRepository.ExecuteUnpackInner(command);

            if (!result.IsSuccess)
            {
                return OperationResult<UnpackInnerResultDto>.Fail(result.Message);
            }

            return OperationResult<UnpackInnerResultDto>.Success(result.Data, "内包拆箱成功");
        }

        public OperationResult<PackInnerResultDto> PackInner(PackInnerRequest request)
        {
            // 1. 参数非空校验
            if (request == null)
                return OperationResult<PackInnerResultDto>.Fail("请求不能为空。");

            if (string.IsNullOrWhiteSpace(request.TargetBoxHuCode))
                return OperationResult<PackInnerResultDto>.Fail("TargetBoxHuCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.InnerHuCode))
                return OperationResult<PackInnerResultDto>.Fail("InnerHuCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.RequestId))
                return OperationResult<PackInnerResultDto>.Fail("RequestId不能为空。");

            if (string.IsNullOrWhiteSpace(request.Operator))
                return OperationResult<PackInnerResultDto>.Fail("Operator不能为空。");

            // 数据标准化
            string targetCode = request.TargetBoxHuCode.Trim().ToUpperInvariant();
            string innerCode = request.InnerHuCode.Trim().ToUpperInvariant();

            // 2. 幂等性校验
            PackInnerResultDto duplicate = _repackRepository.GetPackByRequestId(request.RequestId);
            if (duplicate != null)
            {
                bool sameRequest = string.Equals(duplicate.TargetBoxHuCode, targetCode, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(duplicate.InnerHuCode, innerCode, StringComparison.OrdinalIgnoreCase);

                if (!sameRequest)
                {
                    return OperationResult<PackInnerResultDto>.Fail("RequestId已经被其他装箱业务使用。");
                }

                duplicate.IsDuplicate = true;
                return OperationResult<PackInnerResultDto>.Success(duplicate, "重复请求，返回原装箱结果");
            }

            HuMoveLogDto existingMove = _huLocationRepository.GetMoveByRequestId(request.RequestId);
            if (existingMove != null)
            {
                return OperationResult<PackInnerResultDto>.Fail("RequestId已经被其他移动业务使用。");
            }

            // 3. HU 基础校验
            HuDto target = _huRepository.GetByCode(targetCode);
            HuDto inner = _huRepository.GetByCode(innerCode);

            if (target == null || target.HuType != "OUTER_BOX")
                return OperationResult<PackInnerResultDto>.Fail("目标外箱不存在或HU类型错误。");

            if (inner == null || inner.HuType != "INNER_PACK")
                return OperationResult<PackInnerResultDto>.Fail("内包不存在或HU类型错误。");

            if (target.LifecycleStatus != "ACTIVE" || inner.LifecycleStatus != "ACTIVE")
                return OperationResult<PackInnerResultDto>.Fail("只有ACTIVE状态HU允许装箱。");

            if (target.IsHold || inner.IsHold)
                return OperationResult<PackInnerResultDto>.Fail("参与装箱的HU已被冻结。");

            if (!string.Equals(target.PlantCode, inner.PlantCode, StringComparison.OrdinalIgnoreCase))
                return OperationResult<PackInnerResultDto>.Fail("目标外箱与内包厂别不一致。");

            // 4. 层级与货位校验
            if (_huRepository.HasActiveParent(inner.Id))
                return OperationResult<PackInnerResultDto>.Fail("内包当前已属于其他HU，不能直接装箱。");

            HuLocationDto innerLocation = _huLocationRepository.GetLocation(inner.Id);
            HuLocationDto targetLocation = _huLocationRepository.GetLocation(target.Id);

            if (innerLocation == null || !innerLocation.DirectLocationId.HasValue)
                return OperationResult<PackInnerResultDto>.Fail("内包当前不是独立存放状态。");

            if (targetLocation == null || !targetLocation.DirectLocationId.HasValue)
                return OperationResult<PackInnerResultDto>.Fail("目标外箱没有直接货位。");

            // 5. 构建 Command 并调用持久层
            PackInnerCommand command = new PackInnerCommand
            {
                TargetBoxHuId = target.Id,
                TargetBoxHuCode = target.HuCode,
                InnerHuId = inner.Id,
                InnerHuCode = inner.HuCode,
                PlantCode = inner.PlantCode,
                FromLocationId = innerLocation.DirectLocationId.Value,
                FromLocationCode = innerLocation.DirectLocationCode,
                ToLocationId = targetLocation.DirectLocationId.Value,
                ToLocationCode = targetLocation.DirectLocationCode,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };

            PackInnerPersistenceResult result = _repackRepository.ExecutePackInner(command);

            if (!result.IsSuccess)
            {
                return OperationResult<PackInnerResultDto>.Fail(result.Message);
            }

            return OperationResult<PackInnerResultDto>.Success(result.Data, "内包装箱成功");
        }

        public OperationResult<UnpackBoxResultDto> UnpackBox(UnpackBoxRequest request)
        {
            if (request == null) return OperationResult<UnpackBoxResultDto>.Fail("请求不能为空。");
            request.BoxHuCode = Normalize(request.BoxHuCode);
            request.WarehouseCode = Normalize(request.WarehouseCode);
            request.ToLocationCode = Normalize(request.ToLocationCode);
            request.ScannedLocationCode = Normalize(request.ScannedLocationCode);
            request.RequestId = Text(request.RequestId);
            request.Operator = Text(request.Operator);
            request.BusinessNo = Text(request.BusinessNo);
            request.Remark = Text(request.Remark);
            if (request.BoxHuCode == null) return OperationResult<UnpackBoxResultDto>.Fail("BoxHuCode不能为空。");
            if (request.WarehouseCode == null) return OperationResult<UnpackBoxResultDto>.Fail("WarehouseCode不能为空。");
            if (request.RequestId == null) return OperationResult<UnpackBoxResultDto>.Fail("RequestId不能为空。");
            if (request.Operator == null) return OperationResult<UnpackBoxResultDto>.Fail("Operator不能为空。");
            if (request.BoxHuCode.Length > 100 || request.WarehouseCode.Length > 30 || (request.ToLocationCode != null && request.ToLocationCode.Length > 50) || request.RequestId.Length > 90)
                return OperationResult<UnpackBoxResultDto>.Fail("请求字段长度超限。");

            UnpackBoxResultDto duplicate = _repackRepository.GetUnpackBoxByRequestId(request.RequestId);
            if (duplicate != null)
            {
                if (!string.Equals(duplicate.BoxHuCode, request.BoxHuCode, StringComparison.OrdinalIgnoreCase)
                    || (request.ToLocationCode != null && !string.Equals(duplicate.ToLocationCode, request.ToLocationCode, StringComparison.OrdinalIgnoreCase)))
                    return OperationResult<UnpackBoxResultDto>.Fail("RequestId已经被其他整箱拆包业务使用。");
                duplicate.IsDuplicate = true;
                return OperationResult<UnpackBoxResultDto>.Success(duplicate, "重复请求，返回原整箱拆包结果");
            }

            UnpackBoxCommand command = new UnpackBoxCommand
            {
                BoxHuCode = request.BoxHuCode, WarehouseCode = request.WarehouseCode, ToLocationCode = request.ToLocationCode,
                ScannedLocationCode = request.ScannedLocationCode, RequestId = request.RequestId, BusinessNo = request.BusinessNo,
                Operator = request.Operator, Remark = request.Remark, OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant()
            };
            UnpackBoxPersistenceResult persisted = _repackRepository.ExecuteUnpackBox(command);
            return persisted.IsSuccess
                ? OperationResult<UnpackBoxResultDto>.Success(persisted.Data, "整箱拆包成功")
                : OperationResult<UnpackBoxResultDto>.Fail(persisted.Message);
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
