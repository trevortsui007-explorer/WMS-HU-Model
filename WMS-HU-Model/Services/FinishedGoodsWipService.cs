using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inventory;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class FinishedGoodsWipService : IFinishedGoodsWipService
    {
        private readonly IFinishedGoodsWipRepository _repository;
        public FinishedGoodsWipService(IFinishedGoodsWipRepository repository) { _repository = repository; }

        public OperationResult<FinishedGoodsWipDto> Query(long boxHUId)
        {
            if (boxHUId <= 0) return OperationResult<FinishedGoodsWipDto>.Fail("箱 HU ID 无效。");
            var box = _repository.ReadBox(boxHUId);
            if (box == null) return OperationResult<FinishedGoodsWipDto>.Fail("箱不存在或已失效。");
            if (string.IsNullOrWhiteSpace(box.PlantCode) || box.Groups.Count == 0)
                return OperationResult<FinishedGoodsWipDto>.Fail("箱缺少厂别或有效料号版本信息。");
            foreach (var group in box.Groups)
            {
                if (string.IsNullOrWhiteSpace(group.MaterialCode) || string.IsNullOrWhiteSpace(group.MaterialRevision))
                { group.Message = "箱内料号或版本为空，无法定位流程。"; continue; }
                var flows = _repository.ReadFlows(box.PlantCode, group.MaterialCode, group.MaterialRevision);
                if (flows.Count != 1)
                { group.Message = flows.Count == 0 ? "无启用且未删除的 MES 正常流程。" : "存在多个有效 MES 流程，请先确认流程配置。"; continue; }
                group.FlowId = flows[0];
                var stations = _repository.ReadStations(box.PlantCode, group.FlowId);
                var starts = stations.Where(s => string.Equals(s.StationCode, "PK_01", StringComparison.OrdinalIgnoreCase)).ToList();
                if (starts.Count == 0) { group.Message = "流程中无 PK_01 工站。"; continue; }
                decimal sort;
                if (stations.Any(s => !TrySort(s.Sort, out sort)))
                { group.Message = "流程工站排序无效，请维护数值 sort。"; continue; }
                var start = starts.Min(s => ParseSort(s.Sort));
                var rows = _repository.ReadWip(box.PlantCode, group.MaterialCode, group.MaterialRevision, group.FlowId, start);
                var matched = new List<FinishedGoodsWipStation>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    // The extracted query returns the actual flow-station primary key.
                    // Multiple units are separate rows, not quantities to sum together.
                    var matches = stations.Where(s => !string.IsNullOrWhiteSpace(row.StationId)
                        && SameKey(s.StationId, row.StationId)).ToList();
                    if (matches.Count != 1 || !SameKey(matches[0].StationCode, row.StationCode)
                        || (!string.IsNullOrWhiteSpace(row.FlowItemNo) && !SameKey(matches[0].FlowItemNo, row.FlowItemNo))
                        || string.IsNullOrWhiteSpace(matches[0].StationId)
                        || !seen.Add(matches[0].StationId + "\u001f" + (row.Unit ?? "").Trim()))
                    {
                        group.Message = "MES 流程/WIP 结果与当前流程工站不一致或存在重复，无法确定数量归属。";
                        break;
                    }
                    row.StationId = matches[0].StationId;
                    row.FlowItemNo = matches[0].FlowItemNo;
                    row.Sort = matches[0].Sort;
                    if (ParseSort(row.Sort) >= start) matched.Add(row);
                }
                if (group.Message != null) continue;
                group.Stations = matched.OrderBy(s => ParseSort(s.Sort))
                    .ThenBy(s => s.FlowItemNo, StringComparer.Ordinal).ThenBy(s => s.StationId, StringComparer.Ordinal)
                    .ThenBy(s => s.Unit, StringComparer.Ordinal).ToList();
                if (group.Stations.Count == 0) group.Message = "暂无 PK_01 及后续工站的流程/WIP 数据。";
            }
            return OperationResult<FinishedGoodsWipDto>.Success(box);
        }

        private static bool SameKey(string left, string right)
        {
            return string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySort(string value, out decimal sort)
        {
            return decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture, out sort);
        }
        private static decimal ParseSort(string value) { decimal sort; TrySort(value, out sort); return sort; }
    }
}
