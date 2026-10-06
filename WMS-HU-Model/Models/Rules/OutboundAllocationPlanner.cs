using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Rules
{
    public static class OutboundAllocationPlanner
    {
        private const int MaxCandidateCount = 100;

        public static OutboundAllocationPlanDto Build(List<OutboundAllocationCandidateDto> candidates, decimal requiredQty)
        {
            if (requiredQty <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredQty));

            List<OutboundAllocationCandidateDto> boxes = (candidates ?? new List<OutboundAllocationCandidateDto>())
                .OrderBy(x => x.MinWeekSortKey)
                .ThenBy(x => x.BoxHuId)
                .ToList();

            if (boxes.Count > MaxCandidateCount)
                throw new InvalidOperationException("单个MATNR+UOM分组的OUTER_BOX候选不能超过100个。");

            OutboundAllocationPlanDto plan = new OutboundAllocationPlanDto
            {
                RequiredQty = requiredQty,
                AvailableQty = boxes.Sum(x => x.Qty)
            };

            if (plan.AvailableQty < requiredQty)
            {
                plan.ResultType = "SHORTAGE";
                plan.ShortageQty = requiredQty - plan.AvailableQty;
                return plan;
            }

            List<OutboundAllocationCandidateDto> exactBoxes = FindExactBoxes(boxes, requiredQty);

            if (exactBoxes != null)
            {
                plan.ResultType = "EXACT_WHOLE_BOX";
                plan.SelectedBoxes = exactBoxes;
                return plan;
            }

            List<OutboundInnerPackCandidateDto> innerPacks = boxes
                .SelectMany(x => x.InnerPacks)
                .OrderBy(x => x.MinWeekSortKey)
                .ThenBy(x => x.InnerHuId)
                .ToList();

            if (innerPacks.Count > MaxCandidateCount)
                throw new InvalidOperationException("单个MATNR+UOM分组的INNER_PACK候选不能超过100个。");

            List<OutboundInnerPackCandidateDto> exactInnerPacks = FindExactInnerPacks(innerPacks, requiredQty);

            if (exactInnerPacks != null)
            {
                plan.ResultType = "REPACK_REQUIRED";
                plan.SuggestedInnerPacks = exactInnerPacks;
                return plan;
            }

            plan.ResultType = "NO_EXACT_PACK_COMBINATION";
            return plan;
        }

        private static List<OutboundAllocationCandidateDto> FindExactBoxes(
            List<OutboundAllocationCandidateDto> candidates,
            decimal requiredQty)
        {
            if (candidates.Count == 0)
                return null;

            List<OutboundAllocationCandidateDto> selected = new List<OutboundAllocationCandidateDto>();
            HashSet<string> failed = new HashSet<string>(StringComparer.Ordinal);

            return SearchBoxes(candidates, 0, requiredQty, selected, failed)
                ? new List<OutboundAllocationCandidateDto>(selected)
                : null;
        }

        private static bool SearchBoxes(
            List<OutboundAllocationCandidateDto> candidates,
            int index,
            decimal remaining,
            List<OutboundAllocationCandidateDto> selected,
            HashSet<string> failed)
        {
            if (remaining == 0)
                return true;

            if (remaining < 0 || index >= candidates.Count)
                return false;

            string state = index + "|" + remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (failed.Contains(state))
                return false;

            OutboundAllocationCandidateDto candidate = candidates[index];
            selected.Add(candidate);

            if (SearchBoxes(
                candidates,
                index + 1,
                remaining - candidate.Qty,
                selected,
                failed))
            {
                return true;
            }

            selected.RemoveAt(selected.Count - 1);

            if (SearchBoxes(candidates, index + 1, remaining, selected, failed))
                return true;

            failed.Add(state);
            return false;
        }

        private static List<OutboundInnerPackCandidateDto> FindExactInnerPacks(
            List<OutboundInnerPackCandidateDto> candidates,
            decimal requiredQty)
        {
            if (candidates.Count == 0)
                return null;

            List<OutboundInnerPackCandidateDto> selected = new List<OutboundInnerPackCandidateDto>();
            HashSet<string> failed = new HashSet<string>(StringComparer.Ordinal);

            return SearchInnerPacks(candidates, 0, requiredQty, selected, failed)
                ? new List<OutboundInnerPackCandidateDto>(selected)
                : null;
        }

        private static bool SearchInnerPacks(
            List<OutboundInnerPackCandidateDto> candidates,
            int index,
            decimal remaining,
            List<OutboundInnerPackCandidateDto> selected,
            HashSet<string> failed)
        {
            if (remaining == 0)
                return true;

            if (remaining < 0 || index >= candidates.Count)
                return false;

            string state = index + "|" + remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (failed.Contains(state))
                return false;

            OutboundInnerPackCandidateDto candidate = candidates[index];
            selected.Add(candidate);

            if (SearchInnerPacks(
                candidates,
                index + 1,
                remaining - candidate.Qty,
                selected,
                failed))
            {
                return true;
            }

            selected.RemoveAt(selected.Count - 1);

            if (SearchInnerPacks(candidates, index + 1, remaining, selected, failed))
                return true;

            failed.Add(state);
            return false;
        }
    }
}
