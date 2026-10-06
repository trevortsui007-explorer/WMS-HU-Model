using Learun.Application.WebApi;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Relocation;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class RelocationModule : BaseApi
    {
        private readonly IRelocationService _service;

        public RelocationModule()
            : base("/api/wms-fg/relocation")
        {
            _service = new RelocationService(new RelocationRepository());

            Post["/batch"] = _ =>
            {
                BatchRelocationRequest request = this.Bind<BatchRelocationRequest>();
                OperationResult<BatchRelocationResultDto> result = _service.MoveBatch(request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            };
        }
    }
}
