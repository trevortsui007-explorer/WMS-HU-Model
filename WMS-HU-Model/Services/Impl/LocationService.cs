using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class LocationService : ILocationService
    {
        private static readonly HashSet<string> ProtectedLocationCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "INBOUND-PENDING", "RETURN-PENDING", "OUTBOUND-STAGING", "UNLOCATED", "RETURNED", "SHIPPED"
        };
        private readonly ILocationRepository _repository;

        public LocationService(
            ILocationRepository repository)
        {
            _repository = repository;
        }

        public List<LocationDto> GetList(
            string warehouseCode,
            string zoneCode)
        {
            return _repository.GetList(
                warehouseCode,
                zoneCode);
        }

        public List<LocationDto> GetMaintenanceList(LocationMaintenanceQuery query)
        {
            query = query ?? new LocationMaintenanceQuery();
            query.WarehouseCode = Normalize(query.WarehouseCode);
            query.LocationCode = Normalize(query.LocationCode);
            query.LocationTypeCode = Normalize(query.LocationTypeCode);
            query.MasterStatus = Normalize(query.MasterStatus);
            List<LocationDto> result = _repository.GetMaintenanceList(query);
            foreach (LocationDto location in result) location.IsSystemProtected = IsProtected(location.LocationCode);
            return result;
        }

        public OperationResult<LocationDto> Get(
            string warehouseCode,
            string locationCode)
        {
            var entity =
                _repository.GetByCode(
                    warehouseCode,
                    locationCode);

            if (entity == null)
            {
                return OperationResult<LocationDto>
                    .Fail("货位不存在。");
            }

            return OperationResult<LocationDto>
                .Success(entity);
        }

        public OperationResult<LocationDto> Create(
            CreateLocationRequest request)
        {
            if (request == null)
                return OperationResult<LocationDto>
                    .Fail("请求不能为空。");

            if (string.IsNullOrWhiteSpace(
                request.WarehouseCode))
                return OperationResult<LocationDto>
                    .Fail("WarehouseCode不能为空。");

            if (string.IsNullOrWhiteSpace(
                request.ZoneCode))
                return OperationResult<LocationDto>
                    .Fail("ZoneCode不能为空。");

            if (string.IsNullOrWhiteSpace(
                request.LocationCode))
                return OperationResult<LocationDto>
                    .Fail("LocationCode不能为空。");

            if (string.IsNullOrWhiteSpace(
                request.LocationTypeCode))
                return OperationResult<LocationDto>
                    .Fail("LocationTypeCode不能为空。");

            if (string.IsNullOrWhiteSpace(request.Operator) || request.Operator.Trim().Length > 50)
                return OperationResult<LocationDto>.Fail("Operator不能为空且长度不能超过50。");

            request.WarehouseCode =
                request.WarehouseCode
                    .Trim()
                    .ToUpperInvariant();

            request.ZoneCode =
                request.ZoneCode
                    .Trim()
                    .ToUpperInvariant();

            request.LocationCode =
                request.LocationCode
                    .Trim()
                    .ToUpperInvariant();

            request.LocationTypeCode = Normalize(request.LocationTypeCode);
            request.Operator = request.Operator.Trim();
            if (request.LocationTypeCode != "DOUBLE_RACK") return OperationResult<LocationDto>.Fail("新增货位类型仅支持DOUBLE_RACK。");
            if (IsProtected(request.LocationCode)) return OperationResult<LocationDto>.Fail("系统关键货位只能由部署脚本维护。");
            if (request.WarehouseCode.Length > 30 || request.ZoneCode.Length > 30 || request.LocationCode.Length > 50)
                return OperationResult<LocationDto>.Fail("WarehouseCode、ZoneCode或LocationCode超过数据库长度限制。");
            if (request.LocationName != null && request.LocationName.Length > 200) return OperationResult<LocationDto>.Fail("LocationName长度不能超过200。");
            if (request.ScanCode != null && request.ScanCode.Length > 100) return OperationResult<LocationDto>.Fail("ScanCode长度不能超过100。");
            if (request.Remark != null && request.Remark.Length > 1000) return OperationResult<LocationDto>.Fail("Remark长度不能超过1000。");
            request.IsVirtual = false;
            request.ManageMode = "CAPACITY_LOCATION";
            request.CapacityMode = "STACK_POSITION";
            request.PlantPolicy = "SAME_PLANT";

            if (request.IsScannable &&
                string.IsNullOrWhiteSpace(
                    request.ScanCode))
            {
                request.ScanCode =
                    request.LocationCode;
            }

            if (request.CapacityQty.HasValue &&
                request.CapacityQty.Value < 0)
            {
                return OperationResult<LocationDto>
                    .Fail("CapacityQty不能小于0。");
            }

            if (request.PlantPolicy ==
                "DEDICATED" &&
                string.IsNullOrWhiteSpace(
                    request.DedicatedPlantCode))
            {
                return OperationResult<LocationDto>
                    .Fail(
                    "DEDICATED货位必须指定DedicatedPlantCode。");
            }

            var warehouseId =
                _repository.GetWarehouseId(
                    request.WarehouseCode);

            if (!warehouseId.HasValue)
                return OperationResult<LocationDto>
                    .Fail("仓库不存在或已停用。");

            var zoneId =
                _repository.GetZoneId(
                    warehouseId.Value,
                    request.ZoneCode);

            if (!zoneId.HasValue)
                return OperationResult<LocationDto>
                    .Fail("区域不存在或已停用。");

            if (_repository.Exists(
                warehouseId.Value,
                request.LocationCode))
            {
                return OperationResult<LocationDto>
                    .Fail("货位编码已存在。");
            }

            long? parentLocationId = null;

            if (!string.IsNullOrWhiteSpace(
                request.ParentLocationCode))
            {
                parentLocationId =
                    _repository.GetLocationId(
                        warehouseId.Value,
                        request.ParentLocationCode);

                if (!parentLocationId.HasValue)
                    return OperationResult<LocationDto>
                        .Fail("父货位不存在。");
            }

            _repository.Insert(
                warehouseId.Value,
                zoneId.Value,
                parentLocationId,
                request);

            var entity =
                _repository.GetByCode(
                    request.WarehouseCode,
                    request.LocationCode);

            return OperationResult<LocationDto>
                .Success(
                    entity,
                    "货位创建成功");
        }

        public OperationResult<LocationDto> Update(
            string warehouseCode,
            string locationCode,
            UpdateLocationRequest request)
        {
            warehouseCode = Normalize(warehouseCode);
            locationCode = Normalize(locationCode);
            if (request == null) return OperationResult<LocationDto>.Fail("请求不能为空。");
            var current =
                _repository.GetByCode(
                    warehouseCode,
                    locationCode);

            if (current == null)
                return OperationResult<LocationDto>
                    .Fail("货位不存在。");

            if (IsProtected(locationCode)) return OperationResult<LocationDto>.Fail("系统关键货位不允许编辑。");
            if (string.IsNullOrWhiteSpace(request.Operator) || request.Operator.Trim().Length > 50)
                return OperationResult<LocationDto>.Fail("Operator不能为空且长度不能超过50。");
            request.Operator = request.Operator.Trim();
            request.ZoneCode = Normalize(request.ZoneCode);
            request.LocationTypeCode = Normalize(request.LocationTypeCode);
            if (request.LocationTypeCode != "DOUBLE_RACK") return OperationResult<LocationDto>.Fail("货位类型仅支持DOUBLE_RACK。");
            if (request.LocationName != null && request.LocationName.Length > 200) return OperationResult<LocationDto>.Fail("LocationName长度不能超过200。");
            if (request.ScanCode != null && request.ScanCode.Length > 100) return OperationResult<LocationDto>.Fail("ScanCode长度不能超过100。");
            if (request.Remark != null && request.Remark.Length > 1000) return OperationResult<LocationDto>.Fail("Remark长度不能超过1000。");
            request.ParentLocationCode = current.ParentLocationCode;
            request.LocationLevel = current.LocationLevel;
            request.ManageMode = current.ManageMode;
            request.CapacityMode = current.CapacityMode;
            request.CapacityQty = current.CapacityQty;
            request.MaxWeightKg = current.MaxWeightKg;
            request.PlantPolicy = current.PlantPolicy;
            request.DedicatedPlantCode = current.DedicatedPlantCode;
            request.AllowMixedMaterial = current.AllowMixedMaterial;
            request.AllowMixedBatch = current.AllowMixedBatch;
            request.AllowMixedOrder = current.AllowMixedOrder;
            request.PutawayPriority = current.PutawayPriority;
            request.PickingPriority = current.PickingPriority;
            request.RouteSequence = current.RouteSequence;
            request.MasterStatus = current.MasterStatus;

            var warehouseId =
                _repository.GetWarehouseIdAny(
                    warehouseCode);

            if (!warehouseId.HasValue)
                return OperationResult<LocationDto>
                    .Fail("仓库不存在。");

            var zoneId =
                _repository.GetZoneId(
                    warehouseId.Value,
                    request.ZoneCode);

            if (!zoneId.HasValue)
                return OperationResult<LocationDto>
                    .Fail("区域不存在。");

            long? parentLocationId = null;

            if (!string.IsNullOrWhiteSpace(
                request.ParentLocationCode))
            {
                parentLocationId =
                    _repository.GetLocationId(
                        warehouseId.Value,
                        request.ParentLocationCode);

                if (!parentLocationId.HasValue)
                    return OperationResult<LocationDto>
                        .Fail("父货位不存在。");
            }

            _repository.Update(
                current.Id,
                zoneId.Value,
                parentLocationId,
                request);

            var result =
                _repository.GetByCode(
                    warehouseCode,
                    locationCode);

            return OperationResult<LocationDto>
                .Success(
                    result,
                    "货位修改成功");
        }

        public OperationResult<LocationDto> SetActive(string warehouseCode, string locationCode, bool isActive, MasterDataStatusRequest request)
        {
            string normalizedWarehouse = Normalize(warehouseCode);
            string normalizedLocation = Normalize(locationCode);
            string operatorName = request == null || string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            if (operatorName == null || operatorName.Length > 50) return OperationResult<LocationDto>.Fail("Operator不能为空且长度不能超过50。");
            LocationDto current = _repository.GetByCode(normalizedWarehouse, normalizedLocation);
            if (current == null) return OperationResult<LocationDto>.Fail("货位不存在。");
            if (!isActive && IsProtected(normalizedLocation)) return OperationResult<LocationDto>.Fail("系统关键货位不允许停用。");
            if (!isActive && _repository.CountCurrentHus(current.Id) > 0) return OperationResult<LocationDto>.Fail("该货位当前仍有HU，不允许停用。");
            if (isActive && !_repository.GetWarehouseId(normalizedWarehouse).HasValue) return OperationResult<LocationDto>.Fail("停用仓库下的货位不能启用。");
            _repository.SetMasterStatus(current.Id, isActive ? "ACTIVE" : "INACTIVE", operatorName);
            LocationDto result = _repository.GetByCode(normalizedWarehouse, normalizedLocation);
            result.IsSystemProtected = IsProtected(result.LocationCode);
            return OperationResult<LocationDto>.Success(result, isActive ? "货位已启用。" : "货位已停用。");
        }

        private static bool IsProtected(string locationCode) { return locationCode != null && ProtectedLocationCodes.Contains(locationCode); }
        private static string Normalize(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
    }
}
