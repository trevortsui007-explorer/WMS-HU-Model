using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Models.Inbound
{
    public class FinishedGoodsInboundSnapshot
    {
        public string SourceSystem { get; set; }
        public string SourceType { get; set; }
        public string SourceEntityId { get; set; }
        public string SourceReferenceNo { get; set; }
        public string PlantCode { get; set; }
        public FinishedGoodsInboundPalletSnapshot Pallet { get; set; }
        public List<FinishedGoodsInboundBoxSnapshot> Boxes { get; set; }
    }

    public class FinishedGoodsInboundPalletSnapshot
    {
        public string SourceEntityId { get; set; }
        public string PalletCode { get; set; }
    }

    public class FinishedGoodsInboundBoxSnapshot
    {
        public string SourceEntityId { get; set; }
        public string BoxCode { get; set; }
        public string AlternateBoxCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string Uom { get; set; }
        public string WeekRuleSource { get; set; }
        public string WeekRuleValue { get; set; }
        [JsonIgnore]
        public string MESWeekCode { get; set; }
        [JsonIgnore]
        public string WeekRuleFailureCode { get; set; }
        [JsonIgnore]
        public string WeekRuleFailureMessage { get; set; }
        public List<FinishedGoodsInboundPackSnapshot> InnerPacks { get; set; }
    }

    public class FinishedGoodsInboundPackSnapshot
    {
        public string SourceEntityId { get; set; }
        public string PackId { get; set; }
        public string PackCode { get; set; }
        [JsonIgnore]
        public string LotNo { get; set; }
        [JsonIgnore]
        public bool HasAuthoritativeMesLotNo { get; set; }
        public bool IsSpecialSapCharg { get; set; }
        public string SapBatchNo { get; set; }
        public decimal Qty { get; set; }
        public string DateCode { get; set; }
        public DateTime? PackCreatedTime { get; set; }
        public WeekCodeSnapshot WeekCode { get; set; }
        public bool WeekResolutionSucceeded { get; set; }
        public string WeekFailureCode { get; set; }
        public string WeekFailureMessage { get; set; }
    }

    public class FinishedGoodsInboundReceiveRequest
    {
        public FinishedGoodsInboundSnapshot Snapshot { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class FinishedGoodsInboundCommand
    {
        public FinishedGoodsInboundSnapshot Snapshot { get; set; }
        public string SnapshotHash { get; set; }
        public string HashVersion { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
        public DateTime ReceiveTime { get; set; }
    }

    public class FinishedGoodsInboundResultDto
    {
        public long ReceiptId { get; set; }
        public string SourceSystem { get; set; }
        public string SourceType { get; set; }
        public string SourceEntityId { get; set; }
        public string SnapshotHash { get; set; }
        public string HashVersion { get; set; }
        public DateTime ReceiveTime { get; set; }
        public bool IsDuplicate { get; set; }
    }

    public class FinishedGoodsInboundPersistenceResult
    {
        public bool IsConflict { get; set; }
        public string Message { get; set; }
        public FinishedGoodsInboundResultDto Data { get; set; }
    }
}
