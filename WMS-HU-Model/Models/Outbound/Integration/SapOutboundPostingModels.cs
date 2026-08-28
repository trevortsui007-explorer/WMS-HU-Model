using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WMS_HU_Model.Models.Outbound.Integration
{
    public static class SapOutboundPostingStatuses
    {
        public const string Ready = "READY";
        public const string Sending = "SENDING";
        public const string Success = "SUCCESS";
        public const string Failed = "FAILED";
        public const string Unknown = "UNKNOWN";
    }

    public class SapOutboundPostingPayload
    {
        [JsonProperty("data")]
        public List<SapOutboundPostingRow> Data { get; set; } = new List<SapOutboundPostingRow>();
    }

    public class SapOutboundPostingRow
    {
        [JsonProperty("VBELN")]
        public string VBELN { get; set; }

        [JsonProperty("POSNR")]
        public string POSNR { get; set; }

        [JsonProperty("LGORT")]
        public string LGORT { get; set; }

        [JsonProperty("BOXNO")]
        public string BOXNO { get; set; }

        [JsonProperty("MATNR")]
        public string MATNR { get; set; }

        [JsonProperty("CHARG")]
        public string CHARG { get; set; }

        [JsonProperty("LFIMG")]
        public decimal LFIMG { get; set; }

        [JsonProperty("LFIMG_BOX")]
        public decimal LFIMG_BOX { get; set; }

        [JsonProperty("NTGEW")]
        public decimal NTGEW { get; set; }

        [JsonProperty("BRGEW")]
        public decimal BRGEW { get; set; }

        [JsonProperty("GEWEI")]
        public string GEWEI { get; set; }

        [JsonProperty("VRKME")]
        public string VRKME { get; set; }

        [JsonProperty("BOXSIZE")]
        public string BOXSIZE { get; set; }
    }

    public class SapOutboundTransportConfiguration
    {
        public string Endpoint { get; set; }
        public int TimeoutMs { get; set; }
    }

    public class SapOutboundTransportConfigurationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public SapOutboundTransportConfiguration Configuration { get; set; }
    }

    public class SapOutboundTransportResult
    {
        public bool InvocationStarted { get; set; }
        public string ResultStatus { get; set; }
        public string SapCode { get; set; }
        public string SapMessage { get; set; }
        public string ResponseJson { get; set; }
        public int? HttpStatusCode { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime? ResponseTime { get; set; }
        public long DurationMs { get; set; }
    }

    public class SapOutboundPostingControlRequest
    {
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class SapOutboundManualConfirmRequest : SapOutboundPostingControlRequest
    {
        public string ManualConfirmRemark
        {
            get { return Remark; }
            set { Remark = value; }
        }
    }

    public class SapOutboundPostingPreviewDto
    {
        public string SAPDeliveryNo { get; set; }
        public string PayloadJson { get; set; }
        public string PayloadHash { get; set; }
        public int RowCount { get; set; }
        public int BoxCount { get; set; }
        public decimal TotalQty { get; set; }
        public List<SapOutboundPostingRow> Data { get; set; } = new List<SapOutboundPostingRow>();
        public List<string> Validations { get; set; } = new List<string>();
    }

    public class OutboundShippingStateDto
    {
        public string SAPDeliveryNo { get; set; }
        public string OutboundStatus { get; set; }
        public string SapPostingStatus { get; set; }
        public int PickedBoxCount { get; set; }
        public decimal PickedQty { get; set; }
        public string LastSapCode { get; set; }
        public string LastSapMessage { get; set; }
        public bool CanConfirmShipment { get; set; }
        public bool CanRetry { get; set; }
        public bool RequiresITReview { get; set; }
        public string SuccessConfirmSource { get; set; }
        public string RequestId { get; set; }
    }

    public class SapPostingSnapshotBuildResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public long OutboundOrderId { get; set; }
        public string OutboundStatus { get; set; }
        public SapOutboundPostingPreviewDto Preview { get; set; }
    }

    public class SapPostingClaimResult
    {
        public bool IsSuccess { get; set; }
        public bool ShouldInvokeSap { get; set; }
        public string Message { get; set; }
        public long PostingId { get; set; }
        public long IntegrationLogId { get; set; }
        public int AttemptNo { get; set; }
        public string PayloadJson { get; set; }
        public string PayloadHash { get; set; }
        public OutboundShippingStateDto State { get; set; }
    }

    public class SapIntegrationLogQuery
    {
        public string BusinessKey { get; set; }
        public string InterfaceCode { get; set; }
        public string Direction { get; set; }
        public string ResultStatus { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class SapIntegrationLogDto
    {
        public long Id { get; set; }
        public string InterfaceCode { get; set; }
        public string Direction { get; set; }
        public string BusinessType { get; set; }
        public string BusinessKey { get; set; }
        public string RequestId { get; set; }
        public string CorrelationId { get; set; }
        public int AttemptNo { get; set; }
        public DateTime RequestTime { get; set; }
        public DateTime? ResponseTime { get; set; }
        public long? DurationMs { get; set; }
        public string RequestJson { get; set; }
        public string ResponseJson { get; set; }
        public string ResultStatus { get; set; }
        public string ExternalCode { get; set; }
        public string ExternalMessage { get; set; }
        public int? HttpStatusCode { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string PayloadHash { get; set; }
    }

    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<T> Items { get; set; } = new List<T>();
    }
}
