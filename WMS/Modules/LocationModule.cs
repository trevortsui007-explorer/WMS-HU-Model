using Nancy;
using Nancy.ModelBinding;
using System;
using System.Collections.Generic;
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
                object rawWarehouseCode = Request.Query.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawZoneCode = Request.Query.zoneCode;
                string zoneCode = Convert.ToString(rawZoneCode);

                var list =
                    _service.GetList(
                        warehouseCode,
                        zoneCode);

                return Response.AsJson(
                    ApiResult.Success(list));
            });

            Get("/maintenance", _ =>
            {
                object rawWarehouseCode = Request.Query.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawLocationCode = Request.Query.locationCode;
                string locationCode = Convert.ToString(rawLocationCode);
                object rawLocationTypeCode = Request.Query.locationTypeCode;
                string locationTypeCode = Convert.ToString(rawLocationTypeCode);
                object rawMasterStatus = Request.Query.masterStatus;
                string masterStatus = Convert.ToString(rawMasterStatus);
                List<LocationDto> list = _service.GetMaintenanceList(new LocationMaintenanceQuery
                {
                    WarehouseCode = warehouseCode, LocationCode = locationCode,
                    LocationTypeCode = locationTypeCode, MasterStatus = masterStatus
                });
                return Response.AsJson(ApiResult.Success(list));
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

            Post("/{warehouseCode}/{locationCode}/update", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawLocationCode = args.locationCode;
                string locationCode = Convert.ToString(rawLocationCode);
                UpdateLocationRequest request = this.Bind<UpdateLocationRequest>();
                OperationResult<LocationDto> result = _service.Update(warehouseCode, locationCode, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
            });

            Post("/{warehouseCode}/{locationCode}/enable", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawLocationCode = args.locationCode;
                string locationCode = Convert.ToString(rawLocationCode);
                MasterDataStatusRequest request = this.Bind<MasterDataStatusRequest>();
                OperationResult<LocationDto> result = _service.SetActive(warehouseCode, locationCode, true, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
            });
            Post("/{warehouseCode}/{locationCode}/disable", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawLocationCode = args.locationCode;
                string locationCode = Convert.ToString(rawLocationCode);
                MasterDataStatusRequest request = this.Bind<MasterDataStatusRequest>();
                OperationResult<LocationDto> result = _service.SetActive(warehouseCode, locationCode, false, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
            });
        }
    }
}
