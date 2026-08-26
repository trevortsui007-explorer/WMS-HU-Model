using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Putaway;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class PutawayModule : NancyModule
    {
        private readonly IPutawayService _service;

        public PutawayModule()
            : base("/api/wms/putaway")
        {
            IHuRepository huRepository = new HuRepository();

            ILocationRepository locationRepository = new LocationRepository();

            IHuLocationRepository
                huLocationRepository = new HuLocationRepository();

            IPutawayRepository
                putawayRepository = new PutawayRepository();

            _service =
                new PutawayService(
                    huRepository,
                    locationRepository,
                    huLocationRepository,
                    putawayRepository);

            //----------------------------------------------------
            // 扫描托盘
            //----------------------------------------------------
            Get("/pallet/{palletHuCode}", args =>
            {
                object rawValue = args.palletHuCode;

                string palletHuCode =
                    Convert.ToString(
                        rawValue);

                OperationResult<PalletPutawayStatusDto>
                    result =
                        _service
                            .GetPalletStatus(
                                palletHuCode);

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

            Get("/pallet/by-box/{boxHuCode}", args =>
            {
                object rawValue = args.boxHuCode;
                string boxHuCode = Convert.ToString(rawValue);
                OperationResult<PalletPutawayByBoxContextDto> result = _service.GetPalletContextByBox(boxHuCode);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

            //----------------------------------------------------
            // 扫描外箱并上架
            //----------------------------------------------------
            Post("/box", _ =>
            {
                PutawayBoxRequest request = this.Bind<PutawayBoxRequest>();

                OperationResult<PutawayResultDto>
                    result =
                        _service
                            .PutawayBox(
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

            Post("/pallet", _ =>
            {
                WholePalletPutawayRequest request = this.Bind<WholePalletPutawayRequest>();
                OperationResult<WholePalletPutawayResultDto> result = _service.PutawayPallet(request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });
        }
    }
}
