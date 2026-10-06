using System;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class PackWeekcodeMaintenanceService
    {
        private readonly IPackWeekcodeMaintenanceRepository _repository;
        public PackWeekcodeMaintenanceService(IPackWeekcodeMaintenanceRepository repository) { _repository = repository; }

        public OperationResult<PackWeekcodeForm> Get(string plantCode, string prodNo, string prodRev)
        {
            string error = ScopeError(plantCode, prodNo, prodRev);
            if (error != null) return OperationResult<PackWeekcodeForm>.Fail(error);
            var config = _repository.Get(plantCode.Trim(), prodNo.Trim(), prodRev.Trim())
                ?? new PackWeekcodeConfig { PlantCode = plantCode.Trim(), ProdNo = prodNo.Trim(), ProdRev = prodRev.Trim(), WeekCode = "", IsActive = false };
            config.MESWeekCode = _repository.ReadMESWeekCode(config.PlantCode, config.ProdNo, config.ProdRev);
            return OperationResult<PackWeekcodeForm>.Success(new PackWeekcodeForm
            {
                Config = config,
                Formats = WeekCodeFormats.Common().Concat(_repository.GetSqlFormats()).GroupBy(x => x.Code, StringComparer.Ordinal).Select(x => x.First()).ToList()
            });
        }

        public OperationResult<PackWeekcodeConfig> Save(PackWeekcodeConfig request, string actor)
        {
            if (request == null) return OperationResult<PackWeekcodeConfig>.Fail("维护参数不能为空。");
            string error = ScopeError(request.PlantCode, request.ProdNo, request.ProdRev);
            if (error != null) return OperationResult<PackWeekcodeConfig>.Fail(error);
            if (string.IsNullOrWhiteSpace(actor) || actor.Length > 50) return OperationResult<PackWeekcodeConfig>.Fail("有效操作者不能为空且不能超过50字符。");
            request.WeekCode = (request.WeekCode ?? "").Trim();
            if (request.WeekCode.Length == 0 || request.WeekCode.Length > 100 || request.WeekCode.Any(c => c < 32 || c > 126))
                return OperationResult<PackWeekcodeConfig>.Fail("规则标识必须为1至100个英文、数字或格式符号。");
            if (string.IsNullOrWhiteSpace(request.ExampleValue) || request.ExampleValue.Length > 50)
                return OperationResult<PackWeekcodeConfig>.Fail("请填写原始周期码样例，最多50字符。");
            if ((request.Remark ?? "").Length > 500) return OperationResult<PackWeekcodeConfig>.Fail("说明最多500字符。");
            if (!string.IsNullOrEmpty(request.RowVersion))
            {
                try { if (Convert.FromBase64String(request.RowVersion).Length != 8) return OperationResult<PackWeekcodeConfig>.Fail("并发版本无效，请重新打开表单。"); }
                catch (FormatException) { return OperationResult<PackWeekcodeConfig>.Fail("并发版本无效，请重新打开表单。"); }
            }
            bool supported = WeekCodeFormats.Common().Concat(_repository.GetSqlFormats()).Any(x => x.Code == request.WeekCode);
            if (!supported && request.IsActive) return OperationResult<PackWeekcodeConfig>.Fail("新规则尚未支持解析，请先保存为停用草稿。");
            request.PlantCode = request.PlantCode.Trim(); request.ProdNo = request.ProdNo.Trim(); request.ProdRev = request.ProdRev.Trim();
            // Only the server reads MES; client-supplied MESWeekCode is never authoritative.
            request.MESWeekCode = _repository.ReadMESWeekCode(request.PlantCode, request.ProdNo, request.ProdRev);
            return _repository.Save(request, actor.Trim());
        }

        private static string ScopeError(string plant, string prodNo, string prodRev)
        {
            return string.IsNullOrWhiteSpace(plant) || plant.Trim().Length > 20 || string.IsNullOrWhiteSpace(prodNo) || prodNo.Trim().Length > 50
                || string.IsNullOrWhiteSpace(prodRev) || prodRev.Trim().Length > 30 ? "厂别、料号、版本必填，长度限制分别为20、50、30。" : null;
        }
    }
}
