using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Putaway
{
    public class PalletPutawayByBoxContextDto
    {
        public string ScannedBoxCode { get; set; }
        public string PalletHuCode { get; set; }
        public string WarehouseCode { get; set; }
        public string EffectiveLocationCode { get; set; }
        public string LocationSource { get; set; }
        public int BoxCount { get; set; }
        public decimal TotalQty { get; set; }
        public List<PalletPutawayBoxDto> Boxes { get; set; }

        public PalletPutawayByBoxContextDto()
        {
            Boxes = new List<PalletPutawayBoxDto>();
        }
    }

    public class PalletPutawayBoxDto
    {
        public string BoxCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public decimal Qty { get; set; }
        public string WeekSummary { get; set; }
        public string HuStatus { get; set; }
        public bool IsHold { get; set; }
        public string EffectiveLocationCode { get; set; }
        public string LocationSource { get; set; }
    }

    public class WholePalletPutawayRequest
    {
        public string PalletHuCode { get; set; }
        public string WarehouseCode { get; set; }
        public string TargetLocationCode { get; set; }
        public string ScannedLocationCode { get; set; }
        public string RequestId { get; set; }
        public string BusinessNo { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class WholePalletPutawayCommand
    {
        public string PalletHuCode { get; set; }
        public string WarehouseCode { get; set; }
        public string TargetLocationCode { get; set; }
        public string ScannedLocationCode { get; set; }
        public string RequestId { get; set; }
        public string BusinessNo { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
        public string OperationNo { get; set; }
    }

    public class WholePalletPutawayResultDto
    {
        public string OperationNo { get; set; }
        public string PalletHuCode { get; set; }
        public string FromLocationCode { get; set; }
        public string ToLocationCode { get; set; }
        public List<string> BoxCodes { get; set; }
        public int MovedCount { get; set; }
        public bool IsPalletClosed { get; set; }
        public bool IsDuplicate { get; set; }
        public DateTime PutawayTime { get; set; }

        public WholePalletPutawayResultDto()
        {
            BoxCodes = new List<string>();
        }
    }

    public class WholePalletPutawayPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public WholePalletPutawayResultDto Data { get; set; }

        public static WholePalletPutawayPersistenceResult Success(WholePalletPutawayResultDto data)
        {
            return new WholePalletPutawayPersistenceResult { IsSuccess = true, Data = data };
        }

        public static WholePalletPutawayPersistenceResult Fail(string message)
        {
            return new WholePalletPutawayPersistenceResult { IsSuccess = false, Message = message };
        }
    }
}
