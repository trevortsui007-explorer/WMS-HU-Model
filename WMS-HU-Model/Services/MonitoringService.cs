using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Models.Monitoring;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class MonitoringService : IMonitoringService
    {
        private readonly IMonitoringRepository _repository;
        private readonly IFinishedGoodsInboundReadService _inboundService;
        private readonly IWeekCodeAuditService _weekCodeService;

        public MonitoringService(IMonitoringRepository repository, IFinishedGoodsInboundReadService inboundService, IWeekCodeAuditService weekCodeService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _inboundService = inboundService ?? throw new ArgumentNullException(nameof(inboundService));
            _weekCodeService = weekCodeService ?? throw new ArgumentNullException(nameof(weekCodeService));
        }

        public MonitoringOverviewDto GetOverview()
        {
            DateTime now = DateTime.Now;
            MonitoringOverviewDto result = new MonitoringOverviewDto
            {
                OverallStatus = "RUNNING",
                Service = new DependencyHealthDto { Status = "RUNNING", Message = "WMS服务可访问。", ElapsedMs = 0 },
                ServerTime = now,
                BusinessDateFrom = now.Date,
                BusinessDateToExclusive = now.Date.AddDays(1),
                BusinessStatus = "AVAILABLE",
                BusinessMessage = "自然日内已成功提交的入库记录。",
                FailureHistory = new FailureHistoryCapabilityDto
                {
                    Available = false,
                    Status = "NOT_OBSERVABLE",
                    Message = "当前系统尚未持久化Receipt创建前的接口失败历史。"
                }
            };

            result.WmsDb = Probe(_repository.ProbeWmsDb, "WmsDb");
            result.MesDb = Probe(_repository.ProbeMesDb, "MesDb");
            if (result.WmsDb.Status == "CONNECTED") LoadBusinessSummary(result);
            else SetBusinessUnavailable(result, "WmsDb不可用，业务指标当前无法读取。");

            if (result.WmsDb.Status == "CONNECTED" && result.MesDb.Status == "CONNECTED") LoadWeekCodeCount(result);
            else result.WeekCodeAnomalyCount = null;

            if (result.WmsDb.Status != "CONNECTED" || result.MesDb.Status != "CONNECTED" || result.BusinessStatus != "AVAILABLE" || !result.WeekCodeAnomalyCount.HasValue)
                result.OverallStatus = "DEGRADED";
            return result;
        }

        public MonitoringActivityDto GetActivity(int days)
        {
            int range = days == 14 || days == 30 ? days : 7;
            DateTime to = DateTime.Now.Date.AddDays(1);
            DateTime from = to.AddDays(-range);
            MonitoringActivityDto result = new MonitoringActivityDto { Days = range, FromDate = from, ToDateExclusive = to };
            try
            {
                result.Trend = FillBuckets(from, range, _repository.GetSuccessfulReceiptTrend(from, to));
                result.TrendAvailable = true;
                result.TrendMessage = "仅统计已成功提交的Finished Goods Inbound Receipt。";
            }
            catch
            {
                result.TrendAvailable = false;
                result.TrendMessage = "成功入库趋势当前无法读取。";
            }
            try
            {
                OperationResult<FinishedGoodsInboundRecordResultDto> query = _inboundService.Query(new FinishedGoodsInboundRecordQuery
                {
                    FromTime = from, ToTimeExclusive = to, Page = 1, PageSize = 20
                });
                if (!query.IsSuccess) throw new InvalidOperationException();
                result.RecentReceipts = query.Data.Items.Select(MapReceipt).ToList();
                result.RecentActivityAvailable = true;
                result.RecentActivityMessage = "最近20条已成功提交的入库记录。";
            }
            catch
            {
                result.RecentActivityAvailable = false;
                result.RecentActivityMessage = "最近入库活动当前无法读取。";
            }
            return result;
        }

        private void LoadBusinessSummary(MonitoringOverviewDto result)
        {
            try
            {
                MonitoringBusinessSummaryDto summary = _repository.GetBusinessSummary(result.BusinessDateFrom, result.BusinessDateToExclusive);
                result.TodayReceiptCount = summary.TodayReceiptCount; result.TodayBoxCount = summary.TodayBoxCount;
                result.TodayPackCount = summary.TodayPackCount; result.TodayQty = summary.TodayQty;
                result.ActiveHoldBoxCount = summary.ActiveHoldBoxCount; result.InboundExceptionReceiptCount = summary.InboundExceptionReceiptCount;
            }
            catch { SetBusinessUnavailable(result, "WmsDb可连接，但业务统计当前无法读取。"); }
        }

        private void LoadWeekCodeCount(MonitoringOverviewDto result)
        {
            try
            {
                OperationResult<WeekCodeAuditResultDto> audit = _weekCodeService.Query(new WeekCodeAuditQuery { Page = 1, PageSize = 1 });
                if (audit.IsSuccess) result.WeekCodeAnomalyCount = audit.Data.TotalCount;
            }
            catch { result.WeekCodeAnomalyCount = null; }
        }

        private static DependencyHealthDto Probe(Action action, string name)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                action(); stopwatch.Stop();
                return new DependencyHealthDto { Status = "CONNECTED", Message = name + "连接正常。", ElapsedMs = stopwatch.ElapsedMilliseconds };
            }
            catch
            {
                stopwatch.Stop();
                return new DependencyHealthDto { Status = "UNAVAILABLE", Message = name + "当前不可用。", ElapsedMs = stopwatch.ElapsedMilliseconds };
            }
        }

        private static void SetBusinessUnavailable(MonitoringOverviewDto result, string message)
        {
            result.BusinessStatus = "UNAVAILABLE"; result.BusinessMessage = message;
            result.TodayReceiptCount = null; result.TodayBoxCount = null; result.TodayPackCount = null;
            result.TodayQty = null; result.ActiveHoldBoxCount = null; result.InboundExceptionReceiptCount = null;
        }

        private static List<SuccessfulReceiptTrendPointDto> FillBuckets(DateTime from, int days, IList<SuccessfulReceiptTrendPointDto> rows)
        {
            Dictionary<DateTime, SuccessfulReceiptTrendPointDto> lookup = (rows ?? new List<SuccessfulReceiptTrendPointDto>()).ToDictionary(x => x.Date.Date);
            List<SuccessfulReceiptTrendPointDto> result = new List<SuccessfulReceiptTrendPointDto>();
            for (int index = 0; index < days; index++)
            {
                DateTime date = from.AddDays(index);
                SuccessfulReceiptTrendPointDto point;
                result.Add(lookup.TryGetValue(date, out point) ? point : new SuccessfulReceiptTrendPointDto { Date = date });
            }
            return result;
        }

        private static MonitoringRecentReceiptDto MapReceipt(FinishedGoodsInboundRecordDto item)
        {
            return new MonitoringRecentReceiptDto
            {
                ReceiptId = item.ReceiptId, ReceiveTime = item.ReceiveTime, PlantCode = item.PlantCode,
                SourceSystem = item.SourceSystem, SourceType = item.SourceType, SourceEntityId = item.SourceEntityId,
                BoxCount = item.BoxCount, PackCount = item.InnerPackCount, Qty = item.TotalQty,
                ExceptionBoxCount = item.ExceptionBoxCount, RequestId = item.RequestId
            };
        }
    }
}
