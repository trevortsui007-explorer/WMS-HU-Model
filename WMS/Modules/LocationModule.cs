using Nancy;
using Nancy.ModelBinding;
using System;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class LocationModule : NancyModule
    {
        private readonly ILocationService _service;

        public LocationModule()
            : base("/api/wms/locations")
        {
            _service =
                new LocationService(
                    new LocationRepository());

            // 查询货位列表
            Get("/", _ =>
            {
                string warehouseCode = (string)Request.Query["warehouseCode"];

                string zoneCode = (string)Request.Query["zoneCode"];

                var list =
                    _service.GetList(
                        warehouseCode,
                        zoneCode);

                return Response.AsJson(
                    ApiResult.Success(list));
            });

            // 查询单个货位
            Get("/{warehouseCode}/{locationCode}", args =>
            {
                object rawWarehouseCode = args.warehouseCode;

                object rawLocationCode = args.locationCode;

                string warehouseCode =
                    Convert.ToString(
                        rawWarehouseCode);

                string locationCode =
                    Convert.ToString(
                        rawLocationCode);

                OperationResult<LocationDto> result =
                    _service.Get(
                        warehouseCode,
                        locationCode);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(
                        ApiResult.Fail(
                            result.Message));
                }

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data));
            });

            // 新增货位
            Post("/", _ =>
            {
                CreateLocationRequest request = this.Bind<CreateLocationRequest>();

                var result = _service.Create(request);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(
                        ApiResult.Fail(
                            result.Message));
                }

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data,
                        result.Message));
            });

            // 修改货位
            Put("/{warehouseCode}/{locationCode}", args =>
            {
                object rawWarehouseCode = args.warehouseCode;

                object rawLocationCode = args.locationCode;

                string warehouseCode =
                    Convert.ToString(
                        rawWarehouseCode);

                string locationCode =
                    Convert.ToString(
                        rawLocationCode);

                UpdateLocationRequest request = this.Bind<UpdateLocationRequest>();

                OperationResult<LocationDto> result =
                    _service.Update(
                        warehouseCode,
                        locationCode,
                        request);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(
                        ApiResult.Fail(
                            result.Message));
                }

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data,
                        result.Message));
            });
        }
    }
}
