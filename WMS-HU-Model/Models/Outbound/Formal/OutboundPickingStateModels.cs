using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundPickingStateDto
    {
        public string SAPDeliveryNo { get; set; }
        public string OrderStatus { get; set; }
        public bool HasActivePicking { get; set; }
        public bool IsPickingComplete { get; set; }
        public string AllocationNo { get; set; }
        public string AllocationStatus { get; set; }
        public List<OutboundOrderLineDto> Lines { get; set; }
        public List<PickingStateBatchDto> ActivePickBatches { get; set; }
        public List<PickingStateTaskDto> RemainingTasks { get; set; }
        public List<PickingStateCompletedDetailDto> CompletedPickDetails { get; set; }

        public OutboundPickingStateDto()
        {
            Lines = new List<OutboundOrderLineDto>();
            ActivePickBatches = new List<PickingStateBatchDto>();
            RemainingTasks = new List<PickingStateTaskDto>();
            CompletedPickDetails = new List<PickingStateCompletedDetailDto>();
        }
    }

    public class OutboundPickingStateReadResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public OutboundPickingStateDto Data { get; set; }

        public static OutboundPickingStateReadResult Success(OutboundPickingStateDto data)
        {
            return new OutboundPickingStateReadResult { IsSuccess = true, Data = data };
        }

        public static OutboundPickingStateReadResult Fail(string message)
        {
            return new OutboundPickingStateReadResult { IsSuccess = false, Message = message };
        }
    }

    public class PickingStateBatchDto
    {
        public long PickBatchId { get; set; }
        public string PickBatchNo { get; set; }
        public string BatchStatus { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public decimal PlannedQty { get; set; }
        public decimal PickedQty { get; set; }
        public decimal RemainingQty { get; set; }
    }

    public class PickingStateTaskDto
    {
        public long PickBatchId { get; set; }
        public string PickBatchNo { get; set; }
        public string SAPItemNo { get; set; }
        public List<string> SAPItemNos { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public string Uom { get; set; }
        public string ReservationNo { get; set; }
        public string BoxCode { get; set; }
        public string LocationCode { get; set; }
        public decimal ReservedQty { get; set; }
        public decimal PickedQty { get; set; }
        public decimal RemainingQty { get; set; }
        public string PickStatus { get; set; }
        public int MinWeekSortKey { get; set; }

        public PickingStateTaskDto()
        {
            SAPItemNos = new List<string>();
        }
    }

    public class PickingStateCompletedDetailDto
    {
        public long PickDetailId { get; set; }
        public string PickBatchNo { get; set; }
        public string BoxCode { get; set; }
        public string LocationCode { get; set; }
        public decimal PickedQty { get; set; }
        public string PickStatus { get; set; }
    }
}
