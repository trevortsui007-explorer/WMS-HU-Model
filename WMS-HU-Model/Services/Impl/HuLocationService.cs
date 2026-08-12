using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class HuLocationService : IHuLocationService
    {
        private readonly IHuRepository _huRepository;

        private readonly ILocationRepository _locationRepository;

        private readonly IHuLocationRepository _huLocationRepository;

        public HuLocationService(
            IHuRepository huRepository,
            ILocationRepository locationRepository,
            IHuLocationRepository huLocationRepository)
        {
            _huRepository = huRepository;

            _locationRepository = locationRepository;

            _huLocationRepository = huLocationRepository;
        }

        /// <summary>
        /// 查询HU当前位置
        /// 包含：
        /// 1. DirectLocation
        /// 2. EffectiveLocation
        /// 3. LocationSourceHU
        /// </summary>
        public OperationResult<HuLocationDto> GetLocation(
            string huCode)
        {
            if (string.IsNullOrWhiteSpace(
                huCode))
            {
                return
                    OperationResult<HuLocationDto>
                    .Fail(
                        "HUCode不能为空。");
            }

            string normalizedHuCode =
                huCode
                    .Trim()
                    .ToUpperInvariant();

            HuDto hu =
                _huRepository.GetByCode(
                    normalizedHuCode);

            if (hu == null)
            {
                return
                    OperationResult<HuLocationDto>
                    .Fail(
                        "HU不存在。");
            }

            HuLocationDto location =
                _huLocationRepository
                    .GetLocation(
                        hu.Id);

            if (location == null)
            {
                return
                    OperationResult<HuLocationDto>
                    .Fail(
                        "HU位置信息查询失败。");
            }

            return
                OperationResult<HuLocationDto>
                .Success(
                    location);
        }

        /// <summary>
        /// HU位置移动
        /// </summary>
        public OperationResult<HuMoveResultDto> Move(
            string huCode,
            HuMoveRequest request)
        {
            //--------------------------------------------------
            // 1. 基础参数校验
            //--------------------------------------------------
            if (request == null)
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "请求不能为空。");
            }

            if (string.IsNullOrWhiteSpace(
                huCode))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "HUCode不能为空。");
            }

            string normalizedHuCode =
                huCode
                    .Trim()
                    .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(
                request.WarehouseCode))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "WarehouseCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(
                request.ToLocationCode))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "ToLocationCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(
                request.MoveType))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "MoveType不能为空。");
            }

            //--------------------------------------------------
            // 2. 标准化输入
            //--------------------------------------------------
            string warehouseCode =
                request.WarehouseCode
                    .Trim()
                    .ToUpperInvariant();

            string toLocationCode =
                request.ToLocationCode
                    .Trim()
                    .ToUpperInvariant();

            string moveType =
                request.MoveType
                    .Trim()
                    .ToUpperInvariant();

            //--------------------------------------------------
            // 3. MoveType合法性
            //--------------------------------------------------
            if (!IsSupportedMoveType(
                moveType))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "不支持的MoveType：" +
                        moveType);
            }

            //--------------------------------------------------
            // 4. 查询HU
            //--------------------------------------------------
            HuDto hu =
                _huRepository.GetByCode(
                    normalizedHuCode);

            if (hu == null)
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "HU不存在。");
            }

            //--------------------------------------------------
            // 5. RequestId幂等校验
            //--------------------------------------------------
            if (!string.IsNullOrWhiteSpace(
                request.RequestId))
            {
                HuMoveLogDto existing =
                    _huLocationRepository
                        .GetMoveByRequestId(
                            request.RequestId);

                if (existing != null)
                {
                    //--------------------------------------------------
                    // 同一个RequestId不能用于不同HU
                    //--------------------------------------------------
                    if (!string.Equals(
                        existing.HuCode,
                        normalizedHuCode,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return
                            OperationResult<HuMoveResultDto>
                            .Fail(
                                "RequestId已经被其他HU使用。");
                    }

                    //--------------------------------------------------
                    // 同一个RequestId重复提交
                    // 直接返回第一次移动结果
                    //--------------------------------------------------
                    HuMoveResultDto existingResult = new HuMoveResultDto();

                    existingResult.MoveLogId =
                        existing.Id;

                    existingResult.MoveNo =
                        existing.MoveNo;

                    existingResult.HuCode =
                        existing.HuCode;

                    existingResult.MoveType =
                        existing.MoveType;

                    existingResult.FromLocationCode =
                        existing.FromLocationCode;

                    existingResult.ToLocationCode =
                        existing.ToLocationCode;

                    existingResult.MoveTime =
                        existing.MoveTime;

                    existingResult.Operator =
                        existing.Operator;

                    return
                        OperationResult<HuMoveResultDto>
                        .Success(
                            existingResult,
                            "重复请求，返回原移动结果");
                }
            }

            //--------------------------------------------------
            // 6. HU生命周期校验
            //--------------------------------------------------
            if (!string.Equals(
                hu.LifecycleStatus,
                "ACTIVE",
                StringComparison.OrdinalIgnoreCase))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "只有ACTIVE状态HU允许移动。");
            }

            //--------------------------------------------------
            // 7. HU冻结校验
            //--------------------------------------------------
            if (hu.IsHold)
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "HU当前已冻结，不允许移动。");
            }

            //--------------------------------------------------
            // 8. 有父HU的HU不能单独移动
            //
            // 例如：
            // P001
            // └─ B001
            //
            // B001的位置应该继承P001。
            // 如需单独上架B001，需要先Detach。
            //--------------------------------------------------
            if (_huRepository.HasActiveParent(
                hu.Id))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "HU当前仍存在有效父HU，" +
                        "其位置应继承父HU。" +
                        "如需单独移动，请先执行拆出业务。");
            }

            //--------------------------------------------------
            // 9. 查询目标货位
            //--------------------------------------------------
            LocationDto target =
                _locationRepository.GetByCode(
                    warehouseCode,
                    toLocationCode);

            if (target == null)
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "目标货位不存在。");
            }

            //--------------------------------------------------
            // 10. 目标货位状态
            //--------------------------------------------------
            if (!string.Equals(
                target.MasterStatus,
                "ACTIVE",
                StringComparison.OrdinalIgnoreCase))
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "目标货位当前不可用。");
            }

            //--------------------------------------------------
            // 11. 仓库是否允许该厂别
            //--------------------------------------------------
            bool warehouseAllowsPlant =
                _huLocationRepository
                    .WarehouseAllowsPlant(
                        warehouseCode,
                        hu.PlantCode);

            if (!warehouseAllowsPlant)
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        "当前仓库不允许存储该厂别库存。");
            }

            //--------------------------------------------------
            // 12. DEDICATED厂别货位规则
            //--------------------------------------------------
            if (string.Equals(
                target.PlantPolicy,
                "DEDICATED",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(
                    target.DedicatedPlantCode,
                    hu.PlantCode,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return
                        OperationResult<HuMoveResultDto>
                        .Fail(
                            "目标货位为指定厂别专用货位。");
                }
            }

            //--------------------------------------------------
            // 13. SAME_PLANT货位规则
            //--------------------------------------------------
            if (string.Equals(
                target.PlantPolicy,
                "SAME_PLANT",
                StringComparison.OrdinalIgnoreCase))
            {
                int differentPlantCount =
                    _huLocationRepository
                        .CountDifferentPlantAtLocation(
                            target.Id,
                            hu.PlantCode);

                if (differentPlantCount > 0)
                {
                    return
                        OperationResult<HuMoveResultDto>
                        .Fail(
                            "目标货位当前存在其他厂别库存。");
                }
            }

            //--------------------------------------------------
            // 14. ExpectedFrom并发校验准备
            //--------------------------------------------------
            bool expectedFromSpecified =
                !string.IsNullOrWhiteSpace(
                    request.ExpectedFromLocationCode);

            long? expectedFromLocationId = null;

            if (expectedFromSpecified)
            {
                string expectedFromCode =
                    request.ExpectedFromLocationCode
                        .Trim()
                        .ToUpperInvariant();

                LocationDto expectedLocation =
                    _locationRepository.GetByCode(
                        warehouseCode,
                        expectedFromCode);

                if (expectedLocation == null)
                {
                    return
                        OperationResult<HuMoveResultDto>
                        .Fail(
                            "ExpectedFromLocationCode不存在。");
                }

                expectedFromLocationId = expectedLocation.Id;
            }

            //--------------------------------------------------
            // 15. 构造强类型Repository Command
            //--------------------------------------------------
            HuMoveCommand command = new HuMoveCommand();

            command.HuId =
                hu.Id;

            command.HuCode =
                normalizedHuCode;

            command.ToLocationId =
                target.Id;

            command.ToLocationCode =
                target.LocationCode;

            command.ExpectedFromSpecified =
                expectedFromSpecified;

            command.ExpectedFromLocationId =
                expectedFromLocationId;

            command.MoveNo =
                Guid.NewGuid()
                    .ToString("N")
                    .ToUpperInvariant();

            command.MoveType =
                moveType;

            if (string.IsNullOrWhiteSpace(
                request.ScannedLocationCode))
            {
                command.ScannedLocationCode =
                    target.LocationCode;
            }
            else
            {
                command.ScannedLocationCode =
                    request.ScannedLocationCode
                        .Trim()
                        .ToUpperInvariant();
            }

            command.BusinessNo =
                request.BusinessNo;

            command.TaskNo =
                request.TaskNo;

            command.RequestId =
                request.RequestId;

            command.ReasonCode =
                request.ReasonCode;

            command.Operator =
                request.Operator;

            command.Remark =
                request.Remark;

            //--------------------------------------------------
            // 16. Repository事务执行
            //--------------------------------------------------
            MovePersistenceResult persistResult =
                _huLocationRepository
                    .ExecuteMove(
                        command);

            if (!persistResult.IsSuccess)
            {
                return
                    OperationResult<HuMoveResultDto>
                    .Fail(
                        persistResult.Message);
            }

            //--------------------------------------------------
            // 17. 返回结果
            //--------------------------------------------------
            return
                OperationResult<HuMoveResultDto>
                .Success(
                    persistResult.Data,
                    "HU移动成功");
        }

        /// <summary>
        /// 查询HU移动历史
        /// </summary>
        public OperationResult<List<HuMoveLogDto>> GetMoveLogs(
            string huCode,
            int top)
        {
            //--------------------------------------------------
            // 1. HUCode校验
            //--------------------------------------------------
            if (string.IsNullOrWhiteSpace(
                huCode))
            {
                return
                    OperationResult<List<HuMoveLogDto>>
                    .Fail(
                        "HUCode不能为空。");
            }

            string normalizedHuCode =
                huCode
                    .Trim()
                    .ToUpperInvariant();

            //--------------------------------------------------
            // 2. Top范围保护
            //--------------------------------------------------
            if (top <= 0)
            {
                top = 50;
            }

            if (top > 500)
            {
                top = 500;
            }

            //--------------------------------------------------
            // 3. HU存在性检查
            //--------------------------------------------------
            HuDto hu =
                _huRepository.GetByCode(
                    normalizedHuCode);

            if (hu == null)
            {
                return
                    OperationResult<List<HuMoveLogDto>>
                    .Fail(
                        "HU不存在。");
            }

            //--------------------------------------------------
            // 4. 查询移动历史
            //--------------------------------------------------
            List<HuMoveLogDto> logs =
                _huLocationRepository
                    .GetMoveLogs(
                        hu.Id,
                        top);

            return
                OperationResult<List<HuMoveLogDto>>
                .Success(
                    logs);
        }

        /// <summary>
        /// 当前阶段支持的HU移动类型
        /// </summary>
        private static bool IsSupportedMoveType(
            string moveType)
        {
            return
                moveType == "RECEIVE" ||
                moveType == "PUTAWAY" ||
                moveType == "RELOCATION" ||
                moveType == "RETURN" ||
                moveType == "STAGE" ||
                moveType == "CORRECTION";
        }
    }
}
