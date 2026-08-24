using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class MasterDataMaintenanceModule : NancyModule
    {
        private readonly IMasterDataMaintenanceService _service;

        public MasterDataMaintenanceModule()
            : this(new MasterDataMaintenanceService(new MasterDataMaintenanceRepository()))
        {
        }

        internal MasterDataMaintenanceModule(IMasterDataMaintenanceService service)
            : base("/api/wms/master-data")
        {
            _service = service;

            Get("/options", _ =>
            {
                object rawWarehouseCode = Request.Query.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                return Response.AsJson(ApiResult.Success(_service.GetOptions(warehouseCode)));
            });

            Get("/warehouses", _ =>
            {
                object rawCode = Request.Query.warehouseCode;
                string code = Convert.ToString(rawCode);
                object rawName = Request.Query.warehouseName;
                string name = Convert.ToString(rawName);
                object rawIsActive = Request.Query.isActive;
                string isActiveText = Convert.ToString(rawIsActive);
                return Response.AsJson(ApiResult.Success(_service.QueryWarehouses(new WarehouseQuery
                {
                    WarehouseCode = code, WarehouseName = name, IsActive = NullableBool(isActiveText)
                })));
            });

            Get("/warehouses/{warehouseCode}", args =>
            {
                object rawCode = args.warehouseCode;
                string code = Convert.ToString(rawCode);
                OperationResult<WarehouseDto> result = _service.GetWarehouse(code);
                return Json(result, HttpStatusCode.NotFound);
            });

            Post("/warehouses", _ => Json(_service.CreateWarehouse(this.Bind<WarehouseCreateRequest>()), HttpStatusCode.BadRequest));
            Post("/warehouses/{warehouseCode}/update", args =>
            {
                object rawCode = args.warehouseCode;
                string code = Convert.ToString(rawCode);
                return Json(_service.UpdateWarehouse(code, this.Bind<WarehouseUpdateRequest>()), HttpStatusCode.BadRequest);
            });
            Post("/warehouses/{warehouseCode}/enable", args =>
            {
                object rawCode = args.warehouseCode;
                return WarehouseStatus(rawCode, true);
            });
            Post("/warehouses/{warehouseCode}/disable", args =>
            {
                object rawCode = args.warehouseCode;
                return WarehouseStatus(rawCode, false);
            });

            Get("/warehouse-plants", _ =>
            {
                object rawWarehouseCode = Request.Query.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawPlantCode = Request.Query.plantCode;
                string plantCode = Convert.ToString(rawPlantCode);
                object rawIsActive = Request.Query.isActive;
                string isActiveText = Convert.ToString(rawIsActive);
                return Response.AsJson(ApiResult.Success(_service.QueryWarehousePlants(new WarehousePlantQuery
                {
                    WarehouseCode = warehouseCode, PlantCode = plantCode, IsActive = NullableBool(isActiveText)
                })));
            });

            Get("/warehouse-plants/{warehouseCode}/{plantCode}", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawPlantCode = args.plantCode;
                string plantCode = Convert.ToString(rawPlantCode);
                return Json(_service.GetWarehousePlant(warehouseCode, plantCode), HttpStatusCode.NotFound);
            });

            Post("/warehouse-plants", _ => Json(_service.CreateWarehousePlant(this.Bind<WarehousePlantCreateRequest>()), HttpStatusCode.BadRequest));
            Post("/warehouse-plants/{warehouseCode}/{plantCode}/update", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawPlantCode = args.plantCode;
                string plantCode = Convert.ToString(rawPlantCode);
                return Json(_service.UpdateWarehousePlant(warehouseCode, plantCode, this.Bind<WarehousePlantUpdateRequest>()), HttpStatusCode.BadRequest);
            });
            Post("/warehouse-plants/{warehouseCode}/{plantCode}/enable", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                object rawPlantCode = args.plantCode;
                return WarehousePlantStatus(rawWarehouseCode, rawPlantCode, true);
            });
            Post("/warehouse-plants/{warehouseCode}/{plantCode}/disable", args =>
            {
                object rawWarehouseCode = args.warehouseCode;
                object rawPlantCode = args.plantCode;
                return WarehousePlantStatus(rawWarehouseCode, rawPlantCode, false);
            });
        }

        private Response WarehouseStatus(object rawCode, bool isActive)
        {
            string code = Convert.ToString(rawCode);
            return Json(_service.SetWarehouseActive(code, isActive, this.Bind<MasterDataStatusRequest>()), HttpStatusCode.BadRequest);
        }

        private Response WarehousePlantStatus(object rawWarehouseCode, object rawPlantCode, bool isActive)
        {
            string warehouseCode = Convert.ToString(rawWarehouseCode);
            string plantCode = Convert.ToString(rawPlantCode);
            return Json(_service.SetWarehousePlantActive(warehouseCode, plantCode, isActive, this.Bind<MasterDataStatusRequest>()), HttpStatusCode.BadRequest);
        }

        private Response Json<T>(OperationResult<T> result, HttpStatusCode failureStatus)
        {
            return result.IsSuccess
                ? Response.AsJson(ApiResult.Success(result.Data, result.Message), HttpStatusCode.OK)
                : Response.AsJson(ApiResult.Fail(result.Message, 400), failureStatus);
        }

        private static bool? NullableBool(string value)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : (bool?)null;
        }
    }
}
