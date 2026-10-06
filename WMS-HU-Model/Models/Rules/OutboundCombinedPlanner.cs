using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Rules
{
    public static class OutboundCombinedPlanner
    {
        public static string Build(List<OutboundCombinedBoxDto> boxes, List<OutboundOrderDto> orders)
        {
            if (boxes.Count > 100) throw new InvalidOperationException("候选过多，暂无法生成推荐。");
            if (boxes.Select(x => x.BoxHuId).Distinct().Count() != boxes.Count || boxes.Any(x => x.Qty <= 0))
                throw new InvalidOperationException("候选箱数据异常。");
            foreach (var box in boxes) { box.Selected = false; box.Shares.Clear(); }
            var lines = orders.OrderBy(x => x.SAPDeliveryNo, StringComparer.Ordinal)
                .SelectMany(o => o.Lines.OrderBy(l => l.SAPItemNo, StringComparer.Ordinal)
                    .ThenBy(l => l.Id).Select(l => new { Order = o, Line = l })).ToList();
            if (lines.Count == 0 || lines.Any(x => x.Line.EffectiveRequiredQty <= 0))
                throw new InvalidOperationException("交货单有效需求必须大于零。");
            decimal required = lines.Sum(x => x.Line.EffectiveRequiredQty);
            var sorted = boxes.OrderBy(x => x.WeekSortKey).ThenBy(x => x.BoxHuId).ToList();
            if (sorted.Sum(x => x.Qty) < required) return "SHORTAGE";
            var selected = new List<OutboundCombinedBoxDto>();
            int visited = 0;
            if (!Search(sorted, 0, required, selected, new HashSet<string>(), ref visited)) return "SPLIT_REQUIRED";
            var remaining = lines.ToDictionary(x => x.Line.Id, x => x.Line.EffectiveRequiredQty);
            var usedItems = new HashSet<long>();
            foreach (var box in selected)
            {
                if (box.Items.Count == 0 || box.Items.Sum(x => x.Qty) != box.Qty)
                    throw new InvalidOperationException("箱内库存与箱数量不一致。");
                box.Selected = true;
                foreach (var item in box.Items.OrderBy(x => x.WeekSortKey).ThenBy(x => x.InnerHuId).ThenBy(x => x.HuItemId))
                {
                    if (item.Qty <= 0 || !usedItems.Add(item.HuItemId)) throw new InvalidOperationException("库存明细重复或数量异常。");
                    decimal rest = item.Qty;
                    foreach (var line in lines)
                    {
                        decimal qty = Math.Min(rest, remaining[line.Line.Id]);
                        if (qty <= 0) continue;
                        box.Shares.Add(new OutboundCombinedShareDto {
                            LineId = line.Line.Id, SAPDeliveryNo = line.Order.SAPDeliveryNo, SAPItemNo = line.Line.SAPItemNo,
                            HuItemId = item.HuItemId, InnerHuId = item.InnerHuId, MaterialCode = item.MaterialCode,
                            MaterialRevision = item.MaterialRevision, DateCode = item.DateCode, WeekSortKey = item.WeekSortKey,
                            BatchNo = item.BatchNo, SAPBatchNo = item.SAPBatchNo, Uom = item.Uom, Qty = qty
                        });
                        remaining[line.Line.Id] -= qty; rest -= qty;
                    }
                    if (rest != 0) throw new InvalidOperationException("库存不能完整分摊。");
                }
            }
            if (remaining.Values.Any(x => x != 0)) throw new InvalidOperationException("交货单分摊数量不足。");
            return "EXACT_WHOLE_BOX";
        }

        private static bool Search(List<OutboundCombinedBoxDto> boxes, int index, decimal remaining,
            List<OutboundCombinedBoxDto> selected, HashSet<string> failed, ref int visited)
        {
            if (++visited > 200000) throw new InvalidOperationException("候选过多，暂无法生成推荐。");
            if (remaining == 0) return true;
            if (remaining < 0 || index == boxes.Count) return false;
            string key = index + ":" + remaining.ToString(CultureInfo.InvariantCulture);
            if (failed.Contains(key)) return false;
            selected.Add(boxes[index]);
            if (Search(boxes, index + 1, remaining - boxes[index].Qty, selected, failed, ref visited)) return true;
            selected.RemoveAt(selected.Count - 1);
            if (Search(boxes, index + 1, remaining, selected, failed, ref visited)) return true;
            failed.Add(key); return false;
        }
    }
}
