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
            "INBOUND-PENDING", "INBOUND-CONFIRMED", "RETURN-PENDING", "OUTBOUND-STAGING", "UNLOCATED", "RETURNED", "SHIPPED"
        };
        private static readonly HashSet<string> PhysicalLocationTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DOUBLE_RACK", "TRIPLE_TIER_REMAINDER_RACK", "FLOOR_STORAGE"
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
            query.WarehouseCode = NormalizeCode(query.WarehouseCode);
            query.LocationCode = NormalizeLocationCode(query.LocationCode);
            query.LocationTypeCode = NormalizeCode(query.LocationTypeCode);
            query.MasterStatus = NormalizeCode(query.MasterStatus);
            List<LocationDto> result = _repository.GetMaintenanceList(query);
            foreach (LocationDto location in result) location.IsSystemProtected = IsSystemLocation(location);
            return result;
        }

        public OperationResult<LocationDto> Get(
            string warehouseCode,
            string locationCode)
        {
            warehouseCode = NormalizeCode(warehouseCode);
            locationCode = NormalizeLocationCode(locationCode);
            var entity =
                _repository.GetByCode(
                    warehouseCode,
                    locationCode);

            if (entity == null)
            {
                return OperationResult<LocationDto>
                    .Fail("货位不存在。");
            }

            entity.IsSystemProtected = IsSystemLocation(entity);

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

            request.WarehouseCode = NormalizeCode(request.WarehouseCode);
            request.ZoneCode = NormalizeCode(request.ZoneCode);
            request.LocationCode = NormalizeLocationCode(request.LocationCode);
            request.LocationTypeCode = NormalizeCode(request.LocationTypeCode);
            request.Operator = request.Operator.Trim();
            if (!PhysicalLocationTypes.Contains(request.LocationTypeCode)) return OperationResult<LocationDto>.Fail("请选择有效的物理货位类型。");
            if (request.ZoneCode != "STORAGE") return OperationResult<LocationDto>.Fail("主管只能在存储区新增物理货位。");
            if (IsProtected(request.LocationCode)) return OperationResult<LocationDto>.Fail("系统关键货位只能由部署脚本维护。");
            if (request.WarehouseCode.Length > 20 || request.ZoneCode.Length > 30 || request.LocationCode.Length > 50)
                return OperationResult<LocationDto>.Fail("WarehouseCode、ZoneCode或LocationCode超过数据库长度限制。");
            if (request.LocationName != null && request.LocationName.Length > 100) return OperationResult<LocationDto>.Fail("LocationName长度不能超过100。");
            if (request.Remark != null && request.Remark.Length > 500) return OperationResult<LocationDto>.Fail("Remark长度不能超过500。");
            request.IsVirtual = false;
            request.PlantPolicy = "SAME_PLANT";

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

            _repository.Insert(
                warehouseId.Value,
                zoneId.Value,
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
            warehouseCode = NormalizeCode(warehouseCode);
            locationCode = NormalizeLocationCode(locationCode);
            if (request == null) return OperationResult<LocationDto>.Fail("请求不能为空。");
            var current =
                _repository.GetByCode(
                    warehouseCode,
                    locationCode);

            if (current == null)
                return OperationResult<LocationDto>
                    .Fail("货位不存在。");

            if (IsSystemLocation(current)) return OperationResult<LocationDto>.Fail("系统货位不允许人工修改。");
            if (string.IsNullOrWhiteSpace(request.Operator) || request.Operator.Trim().Length > 50)
                return OperationResult<LocationDto>.Fail("Operator不能为空且长度不能超过50。");
            request.Operator = request.Operator.Trim();
            request.ZoneCode = NormalizeCode(request.ZoneCode);
            request.LocationTypeCode = NormalizeCode(request.LocationTypeCode);
            if (!PhysicalLocationTypes.Contains(request.LocationTypeCode)) return OperationResult<LocationDto>.Fail("请选择有效的物理货位类型。");
            if (request.ZoneCode != "STORAGE") return OperationResult<LocationDto>.Fail("主管只能维护存储区物理货位。");
            if (request.LocationName != null && request.LocationName.Length > 100) return OperationResult<LocationDto>.Fail("LocationName长度不能超过100。");
            if (request.Remark != null && request.Remark.Length > 500) return OperationResult<LocationDto>.Fail("Remark长度不能超过500。");
            request.PlantPolicy = current.PlantPolicy;
            request.DedicatedPlantCode = current.DedicatedPlantCode;
            request.AllowMixedMaterial = current.AllowMixedMaterial;
            request.AllowMixedBatch = current.AllowMixedBatch;
            request.AllowMixedOrder = current.AllowMixedOrder;
            request.PutawayPriority = current.PutawayPriority;
            request.PickingPriority = current.PickingPriority;
            request.RouteSequence = current.RouteSequence;

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

            _repository.Update(
                current.Id,
                zoneId.Value,
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
            string normalizedWarehouse = NormalizeCode(warehouseCode);
            string normalizedLocation = NormalizeLocationCode(locationCode);
            string operatorName = request == null || string.IsNullOrWhiteSpace(request.Operator) ? null : request.Operator.Trim();
            if (operatorName == null || operatorName.Length > 50) return OperationResult<LocationDto>.Fail("Operator不能为空且长度不能超过50。");
            LocationDto current = _repository.GetByCode(normalizedWarehouse, normalizedLocation);
            if (current == null) return OperationResult<LocationDto>.Fail("货位不存在。");
            if (IsSystemLocation(current)) return OperationResult<LocationDto>.Fail("系统货位不允许人工修改状态。");
            if (!isActive && _repository.CountCurrentHus(current.Id) > 0) return OperationResult<LocationDto>.Fail("当前货位仍有库存，请先完成移货，确认货位为空后再下线。");
            if (isActive && !_repository.GetWarehouseId(normalizedWarehouse).HasValue) return OperationResult<LocationDto>.Fail("停用仓库下的货位不能启用。");
            bool updated = _repository.SetActive(current.Id, isActive, operatorName);
            if (!updated && !isActive) return OperationResult<LocationDto>.Fail("当前货位仍有库存，请先完成移货，确认货位为空后再下线。");
            if (!updated) return OperationResult<LocationDto>.Fail("货位状态更新失败。");
            LocationDto result = _repository.GetByCode(normalizedWarehouse, normalizedLocation);
            result.IsSystemProtected = IsSystemLocation(result);
            return OperationResult<LocationDto>.Success(result, isActive ? "货位已启用。" : "货位已下线。");
        }

        private static bool IsProtected(string locationCode) { return locationCode != null && ProtectedLocationCodes.Contains(locationCode); }
        private static bool IsSystemLocation(LocationDto location) { return location != null && (location.IsVirtual || IsProtected(location.LocationCode)); }
        private static string NormalizeCode(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
        private static string NormalizeLocationCode(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    }
}
