using System;
using System.Linq;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Map.Dtos;
using WMS_HU_Model.Map.Import;
using WMS_HU_Model.Map.Repositories.Impl;
using WMS_HU_Model.Map.Services;
using WMS_HU_Model.Map.Services.Impl;
using WMS_HU_Model.Map.Validation;
using WMS_HU_Model.Models.Common;

namespace WMS.Modules.Map
{
    public class MapModule : NancyModule
    {
        private readonly IMapService _service;

        public MapModule()
            : base("/api/wms-fg/map")
        {
            _service = new MapService(new MapRepository(), new MapValidationService(), new MapExcelImportService());

            Get("/layouts", _ =>
            {
                object rawWarehouseCode = Request.Query.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                return Response.AsJson(ApiResult.Success(_service.GetLayouts(warehouseCode)));
            });

            Get("/layouts/{layoutId}", args =>
            {
                object rawLayoutId = args.layoutId;
                long layoutId;
                if (!long.TryParse(Convert.ToString(rawLayoutId), out layoutId)) return Response.AsJson(ApiResult.Fail("layoutId 无效。"), HttpStatusCode.BadRequest);
                OperationResult<MapLayoutDetailDto> result = _service.GetLayout(layoutId);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data)) : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.NotFound);
            });

            Post("/layouts", _ =>
            {
                CreateMapLayoutRequest request = this.Bind<CreateMapLayoutRequest>();
                OperationResult<MapLayoutDto> result = _service.CreateDraft(request);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data, result.Message)) : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.BadRequest);
            });

            Get("/layouts/{layoutId}/validate", args =>
            {
                object rawLayoutId = args.layoutId;
                long layoutId;
                if (!long.TryParse(Convert.ToString(rawLayoutId), out layoutId)) return Response.AsJson(ApiResult.Fail("layoutId 无效。"), HttpStatusCode.BadRequest);
                OperationResult<MapValidationResultDto> result = _service.Validate(layoutId);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data)) : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.BadRequest);
            });

            Post("/layouts/{layoutId}/objects/save", args =>
            {
                object rawLayoutId = args.layoutId;
                long layoutId;
                if (!long.TryParse(Convert.ToString(rawLayoutId), out layoutId)) return Response.AsJson(ApiResult.Fail("layoutId 无效。"), HttpStatusCode.BadRequest);
                SaveMapObjectsRequest request = this.Bind<SaveMapObjectsRequest>();
                OperationResult<MapLayoutDetailDto> result = _service.SaveObjects(layoutId, request);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data, result.Message)) : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.BadRequest);
            });

            Post("/layouts/{layoutId}/import/excel", args =>
            {
                object rawLayoutId = args.layoutId;
                long layoutId;
                if (!long.TryParse(Convert.ToString(rawLayoutId), out layoutId)) return Response.AsJson(ApiResult.Fail("layoutId 无效。"), HttpStatusCode.BadRequest);
                HttpFile file = Request.Files.FirstOrDefault();
                if (file == null) return Response.AsJson(ApiResult.Fail("请选择 Excel 文件。"), HttpStatusCode.BadRequest);
                object rawOperator = Request.Query.operatorName;
                string operatorName = Convert.ToString(rawOperator);
                OperationResult<MapImportResultDto> result = _service.ImportExcel(layoutId, file.Value, file.Name, operatorName);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data)) : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.BadRequest);
            });

            Get("/layout", _ =>
            {
                object rawWarehouseCode = Request.Query.warehouseCode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                OperationResult<MapLayoutDetailDto> result = _service.GetRuntimeLayout(warehouseCode);
                return result.IsSuccess ? Response.AsJson(ApiResult.Success(result.Data)) : Response.AsJson(ApiResult.Fail(result.Message), HttpStatusCode.NotFound);
            });
        }
    }
}
