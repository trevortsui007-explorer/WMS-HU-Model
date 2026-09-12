using Learun.Application.WebApi;
using System;
using System.Collections.Generic;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class HuLocationModule : BaseApi
    {
        private readonly IHuLocationService _service;

        public HuLocationModule()
            : base("/api/wms-fg/hus")
        {
            IHuRepository huRepository = new HuRepository();

            ILocationRepository locationRepository = new LocationRepository();

            IHuLocationRepository
                huLocationRepository = new HuLocationRepository();

            _service =
                new HuLocationService(
                    huRepository,
                    locationRepository,
                    huLocationRepository);

            Get["/{huCode}/location"] = args =>
            {
                object rawHuCode = args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                OperationResult<HuLocationDto> result =
                    _service.GetLocation(
                        huCode);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(
                        ApiResult.Fail(
                            result.Message));
                }

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data));
            };

            Post["/{huCode}/move"] = args =>
            {
                object rawHuCode = args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                HuMoveRequest request = this.Bind<HuMoveRequest>();

                OperationResult<HuMoveResultDto> result =
                    _service.Move(
                        huCode,
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
            };

            Get["/{huCode}/moves"] = args =>
            {
                object rawHuCode = args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                object rawTop = Request.Query["top"];

                string topText =
                    Convert.ToString(
                        rawTop);

                int top = 50;

                int parsedTop;

                if (int.TryParse(
                    topText,
                    out parsedTop))
                {
                    top = parsedTop;
                }

                OperationResult<List<HuMoveLogDto>>
                    result =
                        _service.GetMoveLogs(
                            huCode,
                            top);

                if (!result.IsSuccess)
                {
                    return Response.AsJson(
                        ApiResult.Fail(
                            result.Message));
                }

                return Response.AsJson(
                    ApiResult.Success(
                        result.Data));
            };
        }
    }
}
