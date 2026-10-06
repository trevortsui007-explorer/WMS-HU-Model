using Learun.Application.WebApi;
using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Repack;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class RepackModule : BaseApi
    {
        private readonly IRepackService _service;

        public RepackModule() : base("/api/wms-fg/repack")
        {
            IHuRepository huRepository = new HuRepository();
            IHuLocationRepository huLocationRepository = new HuLocationRepository();
            IRepackRepository repackRepository = new RepackRepository();
            ILocationRepository locationRepository = new LocationRepository();

            _service =
                new RepackService(
                    huRepository,
                    locationRepository,
                    huLocationRepository,
                    repackRepository);

            //----------------------------------------------------
            // 查询外箱
            //----------------------------------------------------
            Get["/boxes/{boxHuCode}"] = args =>
            {
                object rawValue = args.boxHuCode;
                string boxHuCode = Convert.ToString(rawValue);

                OperationResult<BoxRepackStatusDto> result = _service.GetBoxStatus(boxHuCode);

                if (!result.IsSuccess)
                    return Response.AsJson(ApiResult.Fail(result.Message));

                return Response.AsJson(ApiResult.Success(result.Data));
            };

            //----------------------------------------------------
            // 内包箱到箱转移
            //----------------------------------------------------
            Post["/transfer-inner"] = _ =>
            {
                TransferInnerRequest request = this.Bind<TransferInnerRequest>();

                OperationResult<TransferInnerResultDto> result = _service.TransferInner(request);

                if (!result.IsSuccess)
                    return Response.AsJson(ApiResult.Fail(result.Message));

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data,
                        result.Message));
            };

            //----------------------------------------------------
            // 内包箱拆箱转移 -> 到货架
            //----------------------------------------------------
            Post["/unpack-inner"] = _ =>
            {
                UnpackInnerRequest request = this.Bind<UnpackInnerRequest>();

                OperationResult<UnpackInnerResultDto> result = _service.UnpackInner(request);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(ApiResult.Fail(result.Message));
                }

                return Response.AsJson(ApiResult.Success(result.Data, result.Message));
            };

            Post["/unpack-box"] = _ =>
            {
                UnpackBoxRequest request = this.Bind<UnpackBoxRequest>();
                OperationResult<UnpackBoxResultDto> result = _service.UnpackBox(request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            };

            Post["/merge-box"] = _ =>
            {
                MergeBoxRequest request = this.Bind<MergeBoxRequest>();
                OperationResult<MergeBoxResultDto> result = _service.MergeBox(request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            };

            //----------------------------------------------------
            // 内包箱装箱转移 -> 到箱子
            //----------------------------------------------------
            Post["/pack-inner"] = _ =>
            {
                PackInnerRequest request = this.Bind<PackInnerRequest>();

                OperationResult<PackInnerResultDto> result = _service.PackInner(request);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(ApiResult.Fail(result.Message));
                }

                return Response.AsJson(ApiResult.Success(result.Data, result.Message));
            };
        }
    }
}
