using System;
using Learun.Application.WebApi;
using Nancy;
using Nancy.ModelBinding;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Services;

namespace WMS.Modules
{
    public class InboundConfirmModule : BaseApi
    {
        public InboundConfirmModule() : base("/api/wms-fg/inbound-confirm")
        {
            IInboundConfirmService service = new InboundConfirmService(new InboundConfirmRepository());
            Get["/pallet/{palletHuCode}"] = args =>
            {
                object rawValue = args.palletHuCode;
                string palletHuCode = Convert.ToString(rawValue);
                OperationResult<InboundConfirmPalletDto> result = service.GetPallet(palletHuCode);
                return Response.AsJson(result.IsSuccess ? ApiResult.Success(result.Data, result.Message) : ApiResult.Fail(result.Message));
            };
            Post["/box"] = _ =>
            {
                InboundConfirmBoxRequest request = this.Bind<InboundConfirmBoxRequest>();
                OperationResult<InboundConfirmPalletDto> result = service.ConfirmBox(request);
                return Response.AsJson(result.IsSuccess ? ApiResult.Success(result.Data, result.Message) : ApiResult.Fail(result.Message));
            };
        }
    }
}
