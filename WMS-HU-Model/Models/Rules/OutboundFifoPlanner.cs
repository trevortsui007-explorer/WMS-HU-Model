using System;
using System.Collections.Generic;
using WMS_HU_Model.Models.Outbound;

namespace WMS_HU_Model.Rules
{
    /// <summary>
    /// 出库 FIFO 拣货计划规划器
    /// </summary>
    public static class OutboundFifoPlanner
    {
        /// <summary>
        /// 构建 FIFO 批次拣货计划
        /// </summary>
        /// <param name="candidates">按 FIFO 时间排序好的候选外箱列表</param>
        /// <param name="requiredQty">需求数量</param>
        /// <param name="uom">计量单位</param>
        /// <param name="allowOverPick">是否允许整箱超拣</param>
        /// <returns>出库批次计划 DTO</returns>
        public static OutboundBatchPlanDto Build(
            List<OutboundPickCandidateDto> candidates,
            decimal requiredQty,
            string uom,
            bool allowOverPick)
        {
            var plan = new OutboundBatchPlanDto
            {
                RequiredQty = requiredQty,
                Uom = uom,
                AllowOverPick = allowOverPick,
                SelectedBoxes = new List<OutboundBatchPlanItemDto>() // 确保集合不为空
            };

            // 1. 边界防护
            if (candidates == null || candidates.Count == 0 || requiredQty <= 0)
            {
                plan.ResultType = "SHORTAGE";
                plan.PlannedPickQty = 0;
                plan.DifferenceQty = 0;
                plan.RemainingQty = Math.Max(0, requiredQty);
                return plan;
            }

            decimal total = 0;
            int sequence = 0;

            foreach (var candidate in candidates)
            {
                decimal nextTotal = total + candidate.Qty;

                // ----------------------------------------------------
                // 情况 1: 加上当前箱后，累积数量仍未达到需求 -> 继续拿下一箱
                // ----------------------------------------------------
                if (nextTotal < requiredQty)
                {
                    sequence++;
                    AddItem(plan, candidate, sequence, nextTotal);
                    total = nextTotal;
                    continue;
                }

                // ----------------------------------------------------
                // 情况 2: 加上当前箱后，刚好精确满足需求 (Exact Match)
                // ----------------------------------------------------
                if (nextTotal == requiredQty)
                {
                    sequence++;
                    AddItem(plan, candidate, sequence, nextTotal);
                    total = nextTotal;

                    plan.ResultType = "EXACT";
                    plan.PlannedPickQty = total;
                    plan.DifferenceQty = 0;
                    plan.RemainingQty = 0;
                    return plan;
                }

                // ----------------------------------------------------
                // 情况 3: 加上当前箱后会超量 (OverPick)
                // ----------------------------------------------------
                if (allowOverPick)
                {
                    // 允许超拣：取走整箱，记录多出的差额
                    sequence++;
                    AddItem(plan, candidate, sequence, nextTotal);
                    total = nextTotal;

                    plan.ResultType = "OVERPICK";
                    plan.PlannedPickQty = total;
                    plan.DifferenceQty = total - requiredQty;
                    plan.RemainingQty = 0;
                    return plan;
                }

                // 不允许超拣：建议对当前箱进行拆零 (Split)
                plan.ResultType = "NEED_SPLIT";
                plan.PlannedPickQty = total;
                plan.DifferenceQty = 0;
                plan.RemainingQty = requiredQty - total;

                plan.SuggestedSplitBoxHuCode = candidate.BoxHuCode;
                plan.SuggestedSplitBoxQty = candidate.Qty;
                plan.SuggestedSplitQty = requiredQty - total; // 当前箱需拆出的数量

                return plan;
            }

            // ----------------------------------------------------
            // 情况 4: 遍历完所有候选箱，库存仍然不足 (Shortage)
            // ----------------------------------------------------
            plan.ResultType = "SHORTAGE";
            plan.PlannedPickQty = total;
            plan.DifferenceQty = 0;
            plan.RemainingQty = requiredQty - total;

            return plan;
        }

        private static void AddItem(
            OutboundBatchPlanDto plan,
            OutboundPickCandidateDto candidate,
            int sequence,
            decimal cumulativeQty)
        {
            var item = new OutboundBatchPlanItemDto
            {
                SequenceNo = sequence,
                BoxHuId = candidate.BoxHuId,
                BoxHuCode = candidate.BoxHuCode,
                LocationCode = candidate.LocationCode,
                MinWeekSortKey = candidate.MinWeekSortKey,
                FifoTime = candidate.FifoTime,
                BoxQty = candidate.Qty,
                CumulativeQty = cumulativeQty,
                Uom = candidate.Uom
            };

            plan.SelectedBoxes.Add(item);
        }
    }
}
