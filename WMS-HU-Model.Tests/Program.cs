using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WMS_HU_Model.Infrastructure.Sap;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Repositories;
using WMS_HU_Model.Rules;
using WMS_HU_Model.Services;

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
                SapCanonicalHashIgnoresObjectAndRootItemOrder();
                SapCanonicalHashNormalizesDecimalNumbers();
                SapCanonicalHashPreservesPosnrString();
                SapCanonicalHashRejectsTrailingRootValues();
                SapCanonicalHashAllowsTrailingWhitespace();
                SapCanonicalHashPreservesNestedItemArrayOrder();
                SapCanonicalHashIncludesUnknownFields();
                SapReceiveRejectsMalformedJson();
                SapReceiveRejectsInvalidDecimalScaleAndRange();
                SapReceiveRejectsSchemaLengthOverflow();
                SapReceiveAcceptsThirtyCharacterDeliveryNo();
                SapReceiveRejectsOverlongDeliveryNoBeforeExistingCheck();
                Console.WriteLine("Passed " + _passed + " tests.");
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

        private static void SapCanonicalHashIgnoresObjectAndRootItemOrder()
        {
            string first = "{\"VBELN\":\"8300152127\",\"T_ITEM\":[{\"POSNR\":\"000020\",\"LFIMG\":20},{\"POSNR\":\"000010\",\"LFIMG\":10}]}";
            string second = "{\"T_ITEM\":[{\"LFIMG\":10,\"POSNR\":\"000010\"},{\"LFIMG\":20,\"POSNR\":\"000020\"}],\"VBELN\":\"8300152127\"}";
            string firstHash;
            string secondHash;
            AssertCanonicalSuccess(first, out firstHash);
            AssertCanonicalSuccess(second, out secondHash);
            Equal(firstHash, secondHash, "SAP canonical property and item order");
            Pass();
        }

        private static void SapCanonicalHashNormalizesDecimalNumbers()
        {
            string firstHash;
            string secondHash;
            string thirdHash;
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"LFIMG\":7515}", out firstHash);
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"LFIMG\":7515.0}", out secondHash);
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"LFIMG\":7515.0000}", out thirdHash);
            Equal(firstHash, secondHash, "SAP canonical decimal first");
            Equal(firstHash, thirdHash, "SAP canonical decimal second");
            Pass();
        }

        private static void SapCanonicalHashPreservesPosnrString()
        {
            string stringHash;
            string numberHash;
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"T_ITEM\":[{\"POSNR\":\"000010\"}]}", out stringHash);
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"T_ITEM\":[{\"POSNR\":10}]}", out numberHash);
            Equal(false, stringHash == numberHash, "SAP canonical POSNR remains string");
            Pass();
        }

        private static void SapCanonicalHashRejectsTrailingRootValues()
        {
            AssertCanonicalFailure("{\"VBELN\":\"8300152127\"}{\"ignored\":true}", "SAP trailing object");
            AssertCanonicalFailure("{\"VBELN\":\"8300152127\"}0", "SAP trailing scalar");
            Pass();
        }

        private static void SapCanonicalHashAllowsTrailingWhitespace()
        {
            string hash;
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\"} \r\n\t ", out hash);
            Pass();
        }

        private static void SapCanonicalHashPreservesNestedItemArrayOrder()
        {
            string firstHash;
            string secondHash;
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"X\":{\"T_ITEM\":[{\"POSNR\":\"000020\"},{\"POSNR\":\"000010\"}]}}", out firstHash);
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"X\":{\"T_ITEM\":[{\"POSNR\":\"000010\"},{\"POSNR\":\"000020\"}]}}", out secondHash);
            Equal(false, firstHash == secondHash, "SAP nested T_ITEM preserves order");
            Pass();
        }

        private static void SapCanonicalHashIncludesUnknownFields()
        {
            string firstHash;
            string secondHash;
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"UNKNOWN\":\"A\"}", out firstHash);
            AssertCanonicalSuccess("{\"VBELN\":\"8300152127\",\"UNKNOWN\":\"B\"}", out secondHash);
            Equal(false, firstHash == secondHash, "SAP unknown field affects hash");
            Pass();
        }

        private static void SapReceiveRejectsMalformedJson()
        {
            FakeSapReceiveRepository repository = new FakeSapReceiveRepository();
            SapOutboundOrderReceiveService service = new SapOutboundOrderReceiveService(repository);
            OperationResult<SapOutboundOrderReceiveResultDto> result = service.Receive("{bad");
            Equal(false, result.IsSuccess, "SAP malformed JSON rejected");
            Equal(1, repository.RejectedCount, "SAP malformed JSON logged");
            Pass();
        }

        private static void SapReceiveRejectsInvalidDecimalScaleAndRange()
        {
            FakeSapReceiveRepository repository = new FakeSapReceiveRepository();
            SapOutboundOrderReceiveService service = new SapOutboundOrderReceiveService(repository);
            OperationResult<SapOutboundOrderReceiveResultDto> scale = service.Receive(ValidPayload("1.23456", "8300152127"));
            OperationResult<SapOutboundOrderReceiveResultDto> range = service.Receive(ValidPayload("100000000000000", "8300152128"));
            Equal(false, scale.IsSuccess, "SAP decimal scale rejected");
            Equal(false, range.IsSuccess, "SAP decimal range rejected");
            Equal(2, repository.RejectedCount, "SAP decimal validation logged");
            Pass();
        }

        private static void SapReceiveRejectsSchemaLengthOverflow()
        {
            FakeSapReceiveRepository repository = new FakeSapReceiveRepository();
            SapOutboundOrderReceiveService service = new SapOutboundOrderReceiveService(repository);
            OperationResult<SapOutboundOrderReceiveResultDto> result = service.Receive(ValidPayload("1", new string('8', 31)));
            Equal(false, result.IsSuccess, "SAP VBELN max length rejected");
            Equal(1, repository.RejectedCount, "SAP schema length logged");
            Pass();
        }

        private static void SapReceiveAcceptsThirtyCharacterDeliveryNo()
        {
            FakeSapReceiveRepository repository = new FakeSapReceiveRepository();
            SapOutboundOrderReceiveService service = new SapOutboundOrderReceiveService(repository);
            string deliveryNo = new string('3', 30);
            OperationResult<SapOutboundOrderReceiveResultDto> result = service.Receive(ValidPayload("1", deliveryNo));
            Equal(true, result.IsSuccess, "SAP 30 character VBELN continues");
            Equal(1, repository.ExistingCheckCount, "SAP 30 character VBELN existing check");
            Equal(1, repository.CreateCount, "SAP 30 character VBELN create flow");
            Pass();
        }

        private static void SapReceiveRejectsOverlongDeliveryNoBeforeExistingCheck()
        {
            FakeSapReceiveRepository repository = new FakeSapReceiveRepository();
            repository.ExistingResult = new SapOutboundOrderReceiveAttemptResult
            {
                Exists = true,
                IsSuccess = true,
                Message = "重复请求，返回原接收结果。",
                Data = new SapOutboundOrderReceiveResultDto { IsDuplicate = true }
            };
            SapOutboundOrderReceiveService service = new SapOutboundOrderReceiveService(repository);
            string deliveryNo = new string('3', 31);
            string rawJson = ValidPayload("1", deliveryNo);
            OperationResult<SapOutboundOrderReceiveResultDto> result = service.Receive(rawJson);
            Equal(false, result.IsSuccess, "SAP 31 character VBELN rejected");
            Equal(0, repository.ExistingCheckCount, "SAP 31 character VBELN skips existing check");
            Equal(0, repository.CreateCount, "SAP 31 character VBELN skips create");
            Equal(1, repository.RejectedCount, "SAP 31 character VBELN logged");
            Equal(null, repository.RejectedSAPDeliveryNo, "SAP overlong VBELN log key is null");
            Equal(false, string.IsNullOrWhiteSpace(repository.RejectedHash), "SAP overlong VBELN log hash preserved");
            Equal(rawJson, repository.RejectedRawJson, "SAP overlong VBELN raw JSON preserved");
            Pass();
        }

        private static string ValidPayload(string qty, string deliveryNo)
        {
            return "{\"VBELN\":\"" + deliveryNo + "\",\"T_ITEM\":[{\"POSNR\":\"000010\",\"MATNR\":\"M1\",\"LFIMG\":" + qty + ",\"MEINS\":\"PCS\",\"WERKS\":\"1010\"}]}";
        }

        private static void AssertCanonicalSuccess(string rawJson, out string hash)
        {
            JObject payload;
            string error;
            bool success = SapPayloadCanonicalizer.TryCreate(rawJson, out payload, out hash, out error);
            Equal(true, success, "SAP canonical parse");
        }

        private static void AssertCanonicalFailure(string rawJson, string name)
        {
            JObject payload;
            string hash;
            string error;
            bool success = SapPayloadCanonicalizer.TryCreate(rawJson, out payload, out hash, out error);
            Equal(false, success, name);
        }

        private class FakeSapReceiveRepository : ISapOutboundOrderReceiveRepository
        {
            public int RejectedCount { get; private set; }
            public int ExistingCheckCount { get; private set; }
            public int CreateCount { get; private set; }
            public string RejectedSAPDeliveryNo { get; private set; }
            public string RejectedHash { get; private set; }
            public string RejectedRawJson { get; private set; }
            public SapOutboundOrderReceiveAttemptResult ExistingResult { get; set; }

            public SapOutboundOrderReceiveAttemptResult TryHandleExisting(string sapDeliveryNo, string payloadHash, string rawJson)
            {
                ExistingCheckCount++;
                return ExistingResult ?? new SapOutboundOrderReceiveAttemptResult();
            }

            public SapOutboundOrderReceiveAttemptResult CreateFirst(SapOutboundOrderReceiveCommand command)
            {
                CreateCount++;
                return new SapOutboundOrderReceiveAttemptResult
                {
                    IsSuccess = true,
                    Message = "success",
                    Data = new SapOutboundOrderReceiveResultDto()
                };
            }

            public void RecordRejected(string sapDeliveryNo, string payloadHash, string rawJson, string message)
            {
                RejectedCount++;
                RejectedSAPDeliveryNo = sapDeliveryNo;
                RejectedHash = payloadHash;
                RejectedRawJson = rawJson;
            }
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
