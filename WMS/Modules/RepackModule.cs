using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Repack;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class RepackModule : NancyModule
    {
        private readonly IRepackService _service;

        public RepackModule() : base("/api/wms/repack")
        {
            IHuRepository huRepository = new HuRepository();
            IHuLocationRepository huLocationRepository = new HuLocationRepository();
            IRepackRepository repackRepository = new RepackRepository();

            _service = new RepackService(
                huRepository,
                huLocationRepository,
                repackRepository);

            //----------------------------------------------------
            // 查询外箱
            //----------------------------------------------------
            Get("/boxes/{boxHuCode}", args =>
            {
                object rawValue = args.boxHuCode;
                string boxHuCode = Convert.ToString(rawValue);

                OperationResult<BoxRepackStatusDto> result = _service.GetBoxStatus(boxHuCode);

                if (!result.IsSuccess)
                    return Response.AsJson(ApiResult.Fail(result.Message));

                return Response.AsJson(ApiResult.Success(result.Data));
            });

            //----------------------------------------------------
            // 内包箱到箱转移
            //----------------------------------------------------
            Post("/transfer-inner", _ =>
            {
                TransferInnerRequest request = this.Bind<TransferInnerRequest>();

                OperationResult<TransferInnerResultDto> result = _service.TransferInner(request);

                if (!result.IsSuccess)
                    return Response.AsJson(ApiResult.Fail(result.Message));

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data,
                        result.Message));
            });
        }
    }
}