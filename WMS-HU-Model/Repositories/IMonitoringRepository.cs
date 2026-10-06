using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Monitoring;

namespace WMS_HU_Model.Repositories
{
    public interface IMonitoringRepository
    {
        void ProbeWmsDb();
        void ProbeMesDb();
        MonitoringBusinessSummaryDto GetBusinessSummary(DateTime fromTime, DateTime toTimeExclusive);
        List<SuccessfulReceiptTrendPointDto> GetSuccessfulReceiptTrend(DateTime fromTime, DateTime toTimeExclusive);
    }
}
