using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Outbound.Formal
{
    public class OutboundOrderQuery
    {
        public string Status { get; set; }
        public int Top { get; set; }
    }

    public class OutboundOrderDto
    {
        public long Id { get; set; }
        public long WarehouseId { get; set; }
        public string OutboundOrderNo { get; set; }
        public string SAPDeliveryNo { get; set; }
        public string WarehouseCode { get; set; }
        public string PlantCode { get; set; }
        public string SAPStorageLocation { get; set; }
        public string CustomerCode { get; set; }
        public string SoldToCode { get; set; }
        public DateTime? PlannedShipDate { get; set; }
        public string OrderStatus { get; set; }
        public DateTime ReceivedTime { get; set; }
        public DateTime? StartPickTime { get; set; }
        public DateTime? PickedTime { get; set; }
        public DateTime? ShippedTime { get; set; }
        public List<OutboundOrderLineDto> Lines { get; set; }

        public OutboundOrderDto()
        {
            Lines = new List<OutboundOrderLineDto>();
        }
    }

    public class StartPickingRequest
    {
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class StartPickingCommand
    {
        public string SAPDeliveryNo { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class StartPickingResultDto
    {
        public string SAPDeliveryNo { get; set; }
        public string AllocationNo { get; set; }
        public string AllocationStatus { get; set; }
        public DateTime ExpireTime { get; set; }
        public bool IsDuplicate { get; set; }
        public List<StartPickingBatchDto> Batches { get; set; }

        public StartPickingResultDto()
        {
            Batches = new List<StartPickingBatchDto>();
        }
    }

    public class StartPickingBatchDto
    {
        public string PickBatchNo { get; set; }
        public string GroupKey { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public decimal PlannedQty { get; set; }
        public List<StartPickingReservationDto> Reservations { get; set; }

        public StartPickingBatchDto()
        {
            Reservations = new List<StartPickingReservationDto>();
        }
    }

    public class StartPickingReservationDto
    {
        public string ReservationNo { get; set; }
        public long BoxHuId { get; set; }
        public string BoxHuCode { get; set; }
        public string SourceLocationCode { get; set; }
        public decimal ReservedQty { get; set; }
        public int MinWeekSortKey { get; set; }
        public int MaxWeekSortKey { get; set; }
        public string WeekSummary { get; set; }
    }

    public class StartPickingPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public StartPickingResultDto Data { get; set; }

        public static StartPickingPersistenceResult Success(StartPickingResultDto data)
        {
            return new StartPickingPersistenceResult { IsSuccess = true, Data = data };
        }

        public static StartPickingPersistenceResult Fail(string message)
        {
            return new StartPickingPersistenceResult { IsSuccess = false, Message = message };
        }
    }

    public class OutboundActionRequest
    {
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Reason { get; set; }
    }

    public class OutboundActionResultDto
    {
        public string BusinessNo { get; set; }
        public string Status { get; set; }
        public bool IsDuplicate { get; set; }
    }

    public class OutboundActionPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public OutboundActionResultDto Data { get; set; }

        public static OutboundActionPersistenceResult Success(OutboundActionResultDto data)
        {
            return new OutboundActionPersistenceResult { IsSuccess = true, Data = data };
        }

        public static OutboundActionPersistenceResult Fail(string message)
        {
            return new OutboundActionPersistenceResult { IsSuccess = false, Message = message };
        }
    }

    public class OutboundScanRequest
    {
        public string HuCodeOrIdentifier { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class OutboundScanCommand
    {
        public string PickBatchNo { get; set; }
        public long HuId { get; set; }
        public string ScannedValue { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class OutboundScanResultDto
    {
        public long PickDetailId { get; set; }
        public string PickBatchNo { get; set; }
        public string BoxHuCode { get; set; }
        public string FromLocationCode { get; set; }
        public string ToLocationCode { get; set; }
        public decimal BoxQty { get; set; }
        public string Uom { get; set; }
        public string BatchStatus { get; set; }
        public string OrderStatus { get; set; }
        public long MoveLogId { get; set; }
        public string OperationNo { get; set; }
        public bool IsDuplicate { get; set; }
    }

    public class OutboundScanPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public OutboundScanResultDto Data { get; set; }

        public static OutboundScanPersistenceResult Success(OutboundScanResultDto data)
        {
            return new OutboundScanPersistenceResult { IsSuccess = true, Data = data };
        }

        public static OutboundScanPersistenceResult Fail(string message)
        {
            return new OutboundScanPersistenceResult { IsSuccess = false, Message = message };
        }
    }

    public class ReturnPickDetailRequest
    {
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string ExceptionType { get; set; }
        public string ReasonCode { get; set; }
        public string Reason { get; set; }
        public string Remark { get; set; }
    }

    public class ReturnToStockRequest
    {
        public string ToLocationCode { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class ReplacementRequest
    {
        public string ExceptionNo { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class OutboundOrderLineDto
    {
        public long Id { get; set; }
        public long OutboundOrderId { get; set; }
        public string SAPItemNo { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialRevision { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal SpareQty { get; set; }
        public decimal SAPGiftQty { get; set; }
        public decimal GiftQty { get; set; }
        public decimal EffectiveRequiredQty { get; set; }
        public string Uom { get; set; }
        public string PlantCode { get; set; }
        public string SAPStorageLocation { get; set; }
        public decimal AllocatedQty { get; set; }
        public decimal EffectivePickedQty { get; set; }
        public decimal ShippedQty { get; set; }
        public string LineStatus { get; set; }
    }

    public class OutboundPreviewDto
    {
        public string SAPDeliveryNo { get; set; }
        public string OrderStatus { get; set; }
        public List<OutboundPreviewGroupDto> Groups { get; set; }

        public OutboundPreviewDto()
        {
            Groups = new List<OutboundPreviewGroupDto>();
        }
    }

    public class OutboundPreviewGroupDto
    {
        public string GroupKey { get; set; }
        public string MaterialCode { get; set; }
        public string Uom { get; set; }
        public decimal RequiredQty { get; set; }
        public OutboundAllocationPlanDto Plan { get; set; }
    }
}
