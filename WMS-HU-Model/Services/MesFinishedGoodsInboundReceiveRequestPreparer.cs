using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;

namespace WMS_HU_Model.Services
{
    public class MesFinishedGoodsInboundReceiveRequestPreparer
    {
        public OperationResult<FinishedGoodsInboundReceiveRequest> Prepare(MesFinishedGoodsInboundWeekCodePreviewResult preview)
        {
            if (preview == null || preview.Request == null || preview.Request.Snapshot == null || preview.Request.Snapshot.Boxes == null)
                return OperationResult<FinishedGoodsInboundReceiveRequest>.Fail("WeekCode Preview请求无效。");
            if (preview.Packs == null || preview.UnresolvedPackCount > 0 || preview.Packs.Any(x => x == null || string.IsNullOrWhiteSpace(x.BoxSourceEntityId) || !string.Equals(x.Status, "RESOLVED", StringComparison.Ordinal)))
                return OperationResult<FinishedGoodsInboundReceiveRequest>.Fail("存在未解析WeekCode，禁止执行MES入库。");

            Dictionary<string, List<MesFinishedGoodsInboundWeekCodePackPreview>> packsByBox = preview.Packs
                .GroupBy(x => x.BoxSourceEntityId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
            foreach (FinishedGoodsInboundBoxSnapshot box in preview.Request.Snapshot.Boxes)
            {
                List<MesFinishedGoodsInboundWeekCodePackPreview> boxPacks;
                if (box == null || string.IsNullOrWhiteSpace(box.SourceEntityId) || !packsByBox.TryGetValue(box.SourceEntityId, out boxPacks))
                    return OperationResult<FinishedGoodsInboundReceiveRequest>.Fail("Box缺少WeekCode Preview结果。");
                List<string> rulePairs = boxPacks
                    .Select(x => Pair(x.RuleSource, x.RuleValue))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (rulePairs.Count != 1 || string.IsNullOrWhiteSpace(boxPacks[0].RuleSource) || string.IsNullOrWhiteSpace(boxPacks[0].RuleValue))
                    return OperationResult<FinishedGoodsInboundReceiveRequest>.Fail("Box存在不一致的WeekCode Rule。");
                box.WeekRuleSource = boxPacks[0].RuleSource;
                box.WeekRuleValue = boxPacks[0].RuleValue;
            }
            return OperationResult<FinishedGoodsInboundReceiveRequest>.Success(preview.Request);
        }

        private static string Pair(string source, string value)
        {
            return (source ?? string.Empty).Length + ":" + (source ?? string.Empty) + (value ?? string.Empty).Length + ":" + (value ?? string.Empty);
        }
    }
}
