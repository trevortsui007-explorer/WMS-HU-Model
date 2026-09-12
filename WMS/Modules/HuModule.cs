using Learun.Application.WebApi;
using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class HuModule : BaseApi
    {
        private readonly IHuService _service;
        private readonly IHuHoldService _holdService;

        public HuModule()
            : base("/api/wms-fg/hus")
        {
            _service =
                new HuService(
                    new HuRepository());
            _holdService = new HuHoldService(new HuHoldRepository());

            Post["/{huCode}/hold"] = args =>
            {
                object rawHuCode = args.huCode;
                string huCode = Convert.ToString(rawHuCode);
                HoldHuRequest request = this.Bind<HoldHuRequest>();
                OperationResult<HuHoldResultDto> result = _holdService.Hold(huCode, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            };

            Post["/{huCode}/release"] = args =>
            {
                object rawHuCode = args.huCode;
                string huCode = Convert.ToString(rawHuCode);
                ReleaseHuRequest request = this.Bind<ReleaseHuRequest>();
                OperationResult<HuHoldResultDto> result = _holdService.Release(huCode, request);
                return result.IsSuccess
                    ? Response.AsJson(ApiResult.Success(result.Data, result.Message))
                    : Response.AsJson(ApiResult.Fail(result.Message));
            };

            #region 查询 HU

            Get["/{huCode}"] = args =>
            {
                object rawHuCode = args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                OperationResult<HuDto> result =
                    _service.Get(
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

            #endregion

            #region 创建 HU

            Post["/"] = _ =>
            {
                CreateHuRequest request = this.Bind<CreateHuRequest>();

                OperationResult<HuDto> result =
                    _service.Create(
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

            #endregion

            #region 建立 HU 关系

            Post["/attach"] = _ =>
            {
                AttachHuRequest request = this.Bind<AttachHuRequest>();

                OperationResult<bool> result =
                    _service.Attach(
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

            #endregion

            #region 增加库存明细

            Post["/{huCode}/items"] = args =>
            {
                object rawHuCode = args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                AddHuItemRequest request = this.Bind<AddHuItemRequest>();

                OperationResult<HuItemDto> result =
                    _service.AddItem(
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

            #endregion

            #region 查询 HU 树

            Get["/{huCode}/tree"] = args =>
            {
                object rawHuCode = args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                OperationResult<HuTreeNodeDto> result =
                    _service.GetTree(
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

            #endregion
        }
    }
}
