using System;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class HuModule : NancyModule
    {
        private readonly IHuService _service;

        public HuModule()
            : base("/api/wms/hus")
        {
            _service =
                new HuService(
                    new HuRepository());


            #region Get HU

            Get("/{huCode}", args =>
            {
                object rawHuCode =
                    args.huCode;

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
            });

            #endregion


            #region Create HU

            Post("/", _ =>
            {
                CreateHuRequest request =
                    this.Bind<CreateHuRequest>();

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
            });

            #endregion


            #region Attach HU

            Post("/attach", _ =>
            {
                AttachHuRequest request =
                    this.Bind<AttachHuRequest>();

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
            });

            #endregion


            #region Add Item

            Post("/{huCode}/items", args =>
            {
                object rawHuCode =
                    args.huCode;

                string huCode =
                    Convert.ToString(
                        rawHuCode);

                AddHuItemRequest request =
                    this.Bind<AddHuItemRequest>();

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
            });

            #endregion


            #region Get HU Tree

            Get("/{huCode}/tree", args =>
            {
                object rawHuCode =
                    args.huCode;

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
            });

            #endregion
        }
    }
}