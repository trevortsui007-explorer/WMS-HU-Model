using WMS_HU_Model.Models.Common; using WMS_HU_Model.Models.WeekCode;
namespace WMS_HU_Model.Services { public interface IWeekCodeRecalculateService { OperationResult<WeekCodeRecalculateResultDto> Recalculate(WeekCodeRecalculateRequest request); } }
