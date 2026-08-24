using WMS_HU_Model.Models.Monitoring;

namespace WMS_HU_Model.Services
{
    public interface IMonitoringService
    {
        MonitoringOverviewDto GetOverview();
        MonitoringActivityDto GetActivity(int days);
    }
}
