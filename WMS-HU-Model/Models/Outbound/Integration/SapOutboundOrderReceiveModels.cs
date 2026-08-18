using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Integration
{
    public class SapOutboundOrderReceiveRequest
    {
        public string VBELN { get; set; }
        public string LFART { get; set; }
        public string KUNNR { get; set; }
        public string KUNAG { get; set; }
        public string VKORG { get; set; }
        public string WADAT { get; set; }
        public string WAUHR { get; set; }
        public string VSART { get; set; }
        public string SDABW { get; set; }
        public string ERNAM { get; set; }
        public string ERDAT { get; set; }
        public List<SapOutboundOrderReceiveItemRequest> T_ITEM { get; set; }
    }

    public class SapOutboundOrderReceiveItemRequest
    {
        public string POSNR { get; set; }
        public string MATNR { get; set; }
        public decimal LFIMG { get; set; }
        public string MEINS { get; set; }
        public string PSTYV { get; set; }
        public string WERKS { get; set; }
        public string VGBEL { get; set; }
        public string VGPOS { get; set; }
        public string BSTKD { get; set; }
        public string ZKHLH { get; set; }
    }

    public class SapOutboundOrderReceiveResultDto
    {
        public string SAPDeliveryNo { get; set; }
        public string OutboundOrderNo { get; set; }
        public string OrderStatus { get; set; }
        public string WarehouseCode { get; set; }
        public string PlantCode { get; set; }
        public string SAPStorageLocation { get; set; }
        public int LineCount { get; set; }
        public bool IsDuplicate { get; set; }
    }

    public class SapOutboundOrderReceiveCommand
    {
        public string SAPDeliveryNo { get; set; }
        public string PayloadHash { get; set; }
        public string RawJson { get; set; }
        public string DeliveryType { get; set; }
        public string CustomerCode { get; set; }
        public string SoldToCode { get; set; }
        public string SalesOrgCode { get; set; }
        public DateTime? PlannedShipDate { get; set; }
        public TimeSpan? PlannedShipTime { get; set; }
        public string ShippingType { get; set; }
        public string SpecialHandlingCode { get; set; }
        public string SAPCreatedBy { get; set; }
        public DateTime? SAPCreateDate { get; set; }
        public string PlantCode { get; set; }
        public List<SapOutboundOrderReceiveLineCommand> Lines { get; set; }
    }

    public class SapOutboundOrderReceiveLineCommand
    {
        public string SAPItemNo { get; set; }
        public string MaterialCode { get; set; }
        public decimal RequiredQty { get; set; }
        public string Uom { get; set; }
        public string ItemCategory { get; set; }
        public string SourceOrderNo { get; set; }
        public string SourceOrderItem { get; set; }
        public string CustomerPO { get; set; }
    }

    public class SapOutboundOrderReceiveAttemptResult
    {
        public bool Exists { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public SapOutboundOrderReceiveResultDto Data { get; set; }
    }
}
