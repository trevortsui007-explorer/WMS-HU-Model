using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Rules;

namespace WMS_HU_Model.Tests
{
    internal static class Program
    {
        private static int _passed;

        private static int Main()
        {
            try
            {
                ExactWholeBoxMaySkipUnusableEarliestBox();
                ExactWholeBoxUsesStableHuOrder();
                RepackRequiredUsesWholeInnerPacks();
                ShortageIsReportedBeforeCombinationSearch();
                NoExactPackCombinationIsReported();
                CandidateLimitIsEnforced();
                Console.WriteLine("Passed " + _passed + " outbound planner tests.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void ExactWholeBoxMaySkipUnusableEarliestBox()
        {
            OutboundAllocationPlanDto plan = OutboundAllocationPlanner.Build(new List<OutboundAllocationCandidateDto>
            {
                Box(1, 7, 202401),
                Box(2, 5, 202402),
                Box(3, 5, 202402)
            }, 10);
            Equal("EXACT_WHOLE_BOX", plan.ResultType, "exact result");
            Equal("2,3", string.Join(",", plan.SelectedBoxes.Select(x => x.BoxHuId)), "selected boxes");
            Pass();
        }

        private static void ExactWholeBoxUsesStableHuOrder()
        {
            OutboundAllocationPlanDto plan = OutboundAllocationPlanner.Build(new List<OutboundAllocationCandidateDto>
            {
                Box(9, 10, 202401),
                Box(3, 10, 202401)
            }, 10);
            Equal(3L, plan.SelectedBoxes.Single().BoxHuId, "stable HU tie-break");
            Pass();
        }

        private static void RepackRequiredUsesWholeInnerPacks()
        {
            OutboundAllocationCandidateDto box = Box(1, 100, 202401);
            box.InnerPacks.Add(Inner(11, 40, 202401));
            box.InnerPacks.Add(Inner(12, 30, 202401));
            box.InnerPacks.Add(Inner(13, 30, 202402));
            OutboundAllocationPlanDto plan = OutboundAllocationPlanner.Build(
                new List<OutboundAllocationCandidateDto> { box },
                70);
            Equal("REPACK_REQUIRED", plan.ResultType, "repack result");
            Equal(70m, plan.SuggestedInnerPacks.Sum(x => x.Qty), "repack quantity");
            Pass();
        }

        private static void ShortageIsReportedBeforeCombinationSearch()
        {
            OutboundAllocationPlanDto plan = OutboundAllocationPlanner.Build(
                new List<OutboundAllocationCandidateDto> { Box(1, 40, 202401) },
                50);
            Equal("SHORTAGE", plan.ResultType, "shortage result");
            Equal(10m, plan.ShortageQty, "shortage quantity");
            Pass();
        }

        private static void NoExactPackCombinationIsReported()
        {
            OutboundAllocationCandidateDto box = Box(1, 100, 202401);
            box.InnerPacks.Add(Inner(11, 40, 202401));
            box.InnerPacks.Add(Inner(12, 30, 202402));
            box.InnerPacks.Add(Inner(13, 30, 202402));
            OutboundAllocationPlanDto plan = OutboundAllocationPlanner.Build(
                new List<OutboundAllocationCandidateDto> { box },
                50);
            Equal("NO_EXACT_PACK_COMBINATION", plan.ResultType, "no combination result");
            Pass();
        }

        private static void CandidateLimitIsEnforced()
        {
            List<OutboundAllocationCandidateDto> boxes = Enumerable.Range(1, 101)
                .Select(x => Box(x, 1, 202401))
                .ToList();
            bool threw = false;
            try
            {
                OutboundAllocationPlanner.Build(boxes, 1);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Equal(true, threw, "candidate limit");
            Pass();
        }

        private static OutboundAllocationCandidateDto Box(long id, decimal qty, int week)
        {
            return new OutboundAllocationCandidateDto
            {
                BoxHuId = id,
                BoxHuCode = "B" + id,
                Qty = qty,
                MinWeekSortKey = week,
                MaxWeekSortKey = week,
                MaterialCode = "M1",
                Uom = "PCS"
            };
        }

        private static OutboundInnerPackCandidateDto Inner(long id, decimal qty, int week)
        {
            return new OutboundInnerPackCandidateDto
            {
                InnerHuId = id,
                InnerHuCode = "I" + id,
                Qty = qty,
                MinWeekSortKey = week
            };
        }

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(name + ": expected " + expected + ", actual " + actual + ".");
        }

        private static void Pass()
        {
            _passed++;
        }
    }
}
