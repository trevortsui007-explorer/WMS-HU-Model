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
        private readonly IPutawayRecommendationService _recommendationService;

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

            _recommendationService = new PutawayRecommendationService(new PutawayRecommendationRepository());

            Get("/recommendations", _ =>
            {
                object rawWarehouseCode = Request.Query.warehouseCode;
                object rawBoxHuCode = Request.Query.boxHuCode;
                object rawItMode = Request.Query.itMode;
                string warehouseCode = Convert.ToString(rawWarehouseCode);
                string boxHuCode = Convert.ToString(rawBoxHuCode);
                string itModeText = Convert.ToString(rawItMode);
                bool itMode = string.Equals((itModeText ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals((itModeText ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase);
                OperationResult<PutawayRecommendationResultDto> result = _recommendationService.GetRecommendations(warehouseCode, boxHuCode, itMode);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            });

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
