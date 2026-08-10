using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class LocationService : ILocationService
    {
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
            var current =
                _repository.GetByCode(
                    warehouseCode,
                    locationCode);

            if (current == null)
                return OperationResult<LocationDto>
                    .Fail("货位不存在。");


            var warehouseId =
                _repository.GetWarehouseId(
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
    }
}