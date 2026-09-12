using Learun.Application.WebApi;
using System;
using Nancy;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inventory;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class InventoryQueryModule : BaseApi
    {
        private readonly IInventoryQueryService _service;

        public InventoryQueryModule()
            : this(new InventoryQueryService(
                new InventoryQueryRepository(),
                new HuIdentifierService(new HuIdentifierRepository()),
                new EffectiveLocationResolver()))
        {
        }

        internal InventoryQueryModule(IInventoryQueryService service)
            : base("/api/wms-fg/inventory")
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            Get["/lookup"] = _ =>
            {
                object rawWarehouseCode = Request.Query["warehouseCode"];
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                object rawCode = Request.Query["code"];
                string code = Convert.ToString(rawCode);

                OperationResult<InventoryLookupDto> result = _service.Lookup(warehouseCode, code);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data), HttpStatusCode.OK)
                    : Response.AsJson(ApiResult.Fail(result.Message, 400), HttpStatusCode.BadRequest);
            };
        }
    }
}
