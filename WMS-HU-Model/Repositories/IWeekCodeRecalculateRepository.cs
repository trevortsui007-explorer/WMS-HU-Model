using System.Collections.Generic; using WMS_HU_Model.Models.WeekCode;
namespace WMS_HU_Model.Repositories { public interface IWeekCodeRecalculateRepository { List<WeekCodeRecalculateBoxFact> GetCurrentBoxes(string plant,string material,string revision); WeekCodeRecalculateBoxResultDto ApplyBox(WeekCodeRecalculateBoxPlan plan, WeekCodeRecalculateRequest request); } }
