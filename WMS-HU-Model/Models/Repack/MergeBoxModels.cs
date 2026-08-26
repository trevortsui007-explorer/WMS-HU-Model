using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Repack
{
    public class MergeBoxRequest
    {
        public string WarehouseCode { get; set; }
        public List<string> InnerPackHuCodes { get; set; }
        public string RequestId { get; set; }
        public string BusinessNo { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }

        public MergeBoxRequest()
        {
            InnerPackHuCodes = new List<string>();
        }
    }

    public class MergeBoxCommand : MergeBoxRequest
    {
        public string OperationNo { get; set; }
    }

    public class MergeBoxResultDto
    {
        public string NewBoxHuCode { get; set; }
        public string PlantCode { get; set; }
        public string WarehouseCode { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public int InnerPackCount { get; set; }
        public decimal TotalQty { get; set; }
        public List<string> InnerPackHuCodes { get; set; }
        public string LocationCode { get; set; }
        public string ZoneCode { get; set; }
        public bool IsDuplicate { get; set; }

        public MergeBoxResultDto()
        {
            InnerPackHuCodes = new List<string>();
        }
    }

    public class MergeBoxPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public MergeBoxResultDto Data { get; set; }

        public static MergeBoxPersistenceResult Success(MergeBoxResultDto data)
        {
            return new MergeBoxPersistenceResult { IsSuccess = true, Message = "success", Data = data };
        }

        public static MergeBoxPersistenceResult Fail(string message)
        {
            return new MergeBoxPersistenceResult { IsSuccess = false, Message = message };
        }
    }

    public class MergeBoxPackFact
    {
        public long HuId { get; set; }
        public string HuCode { get; set; }
        public string HuType { get; set; }
        public string PlantCode { get; set; }
        public string LifecycleStatus { get; set; }
        public bool IsHold { get; set; }
        public bool HasActiveParent { get; set; }
        public long? LocationId { get; set; }
        public string LocationCode { get; set; }
        public string WarehouseCode { get; set; }
        public string WarehouseStatus { get; set; }
        public string ZoneCode { get; set; }
        public string ZoneStatus { get; set; }
        public string LocationStatus { get; set; }
        public bool IsVirtual { get; set; }
        public int ActiveItemCount { get; set; }
        public int MaterialCodeCount { get; set; }
        public int MaterialRevisionCount { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public decimal TotalQty { get; set; }
    }
}
