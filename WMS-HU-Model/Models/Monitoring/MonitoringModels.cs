using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Monitoring
{
    public class DependencyHealthDto
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public long ElapsedMs { get; set; }
    }

    public class FailureHistoryCapabilityDto
    {
        public bool Available { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class MonitoringBusinessSummaryDto
    {
        public int TodayReceiptCount { get; set; }
        public int TodayBoxCount { get; set; }
        public int TodayPackCount { get; set; }
        public decimal TodayQty { get; set; }
        public int ActiveHoldBoxCount { get; set; }
        public int InboundExceptionReceiptCount { get; set; }
    }

    public class MonitoringOverviewDto
    {
        public string OverallStatus { get; set; }
        public DependencyHealthDto Service { get; set; }
        public DependencyHealthDto WmsDb { get; set; }
        public DependencyHealthDto MesDb { get; set; }
        public DateTime ServerTime { get; set; }
        public DateTime BusinessDateFrom { get; set; }
        public DateTime BusinessDateToExclusive { get; set; }
        public string BusinessStatus { get; set; }
        public string BusinessMessage { get; set; }
        public int? TodayReceiptCount { get; set; }
        public int? TodayBoxCount { get; set; }
        public int? TodayPackCount { get; set; }
        public decimal? TodayQty { get; set; }
        public int? ActiveHoldBoxCount { get; set; }
        public int? WeekCodeAnomalyCount { get; set; }
        public int? InboundExceptionReceiptCount { get; set; }
        public FailureHistoryCapabilityDto FailureHistory { get; set; }
    }

    public class SuccessfulReceiptTrendPointDto
    {
        public DateTime Date { get; set; }
        public int ReceiptCount { get; set; }
        public int BoxCount { get; set; }
        public int PackCount { get; set; }
        public decimal Qty { get; set; }
    }

    public class MonitoringRecentReceiptDto
    {
        public long ReceiptId { get; set; }
        public DateTime ReceiveTime { get; set; }
        public string PlantCode { get; set; }
        public string SourceSystem { get; set; }
        public string SourceType { get; set; }
        public string SourceEntityId { get; set; }
        public int BoxCount { get; set; }
        public int PackCount { get; set; }
        public decimal Qty { get; set; }
        public int ExceptionBoxCount { get; set; }
        public string RequestId { get; set; }
    }

    public class MonitoringActivityDto
    {
        public int Days { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDateExclusive { get; set; }
        public bool TrendAvailable { get; set; }
        public string TrendMessage { get; set; }
        public List<SuccessfulReceiptTrendPointDto> Trend { get; set; } = new List<SuccessfulReceiptTrendPointDto>();
        public bool RecentActivityAvailable { get; set; }
        public string RecentActivityMessage { get; set; }
        public List<MonitoringRecentReceiptDto> RecentReceipts { get; set; } = new List<MonitoringRecentReceiptDto>();
    }
}
