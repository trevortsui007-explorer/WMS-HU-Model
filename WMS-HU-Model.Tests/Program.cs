using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using WMS_HU_Model.Infrastructure.Sap;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Models.WeekCode;
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
                FinishedGoodsInboundStructuralValidationRejectsDuplicateBoxCode();
                FinishedGoodsInboundStructuralValidationGuardsBarcodeNamespace();
                FinishedGoodsInboundStructuralValidationGuardsPalletBarcodeNamespace();
                FinishedGoodsInboundPreparedValidationRequiresWeekCode();
                FinishedGoodsInboundWeekCodeResolverSupportsAliasesAndYww();
                FinishedGoodsInboundHasherIsCanonicalAndVersioned();
                FinishedGoodsInboundServiceReturnsDuplicateAndConflict();
                FinishedGoodsInboundDuplicateSkipsWeekResolution();
                FinishedGoodsInboundServicePropagatesPersistenceFailure();
                FinishedGoodsInboundExceptionIsPreparedAndPersisted();
                FinishedGoodsInboundMissingDateCodesAreExceptionInbound();
                FinishedGoodsInboundRequiresActor();
                FinishedGoodsInboundHashAndMoveIdsAreStable();
                MesFinishedGoodsInboundSnapshotBuilderValidatesAndMaps();
                MesWeekCodeRuleProviderUsesDistinctValues();
                MesWeekCodePreviewContinuesAfterErrors();
                MesFinishedGoodsInboundReceiveRequestPreparerUsesBoxSourceIdentity();
                MesProductionOrchestrationUsesReceiveAndHoldPolicy();
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

        private static void FinishedGoodsInboundStructuralValidationRejectsDuplicateBoxCode()
        {
            FinishedGoodsInboundSnapshot snapshot = InboundSnapshot();
            snapshot.Boxes.Add(new FinishedGoodsInboundBoxSnapshot
            {
                SourceEntityId = "B2", BoxCode = "BOX1", MaterialCode = "M1", Uom = "PCS", WeekRuleSource = "TEST", WeekRuleValue = "YYWW",
                InnerPacks = new List<FinishedGoodsInboundPackSnapshot> { new FinishedGoodsInboundPackSnapshot { SourceEntityId = "P2", PackCode = "PACK2", Qty = 1, DateCode = "2627" } }
            });
            Equal(false, string.IsNullOrWhiteSpace(FinishedGoodsInboundSnapshotValidator.ValidateStructural(snapshot)), "inbound duplicate box rejected");
            Pass();
        }

        private static void FinishedGoodsInboundPreparedValidationRequiresWeekCode()
        {
            FinishedGoodsInboundSnapshot snapshot = InboundSnapshot();
            Equal(true, string.IsNullOrWhiteSpace(FinishedGoodsInboundSnapshotValidator.ValidatePrepared(snapshot)), "inbound unresolved week is permitted");
            Pass();
        }

        private static void FinishedGoodsInboundStructuralValidationGuardsBarcodeNamespace()
        {
            FinishedGoodsInboundSnapshot allowed = InboundSnapshot();
            allowed.Boxes[0].AlternateBoxCode = "BOX1";
            Equal(true, string.IsNullOrWhiteSpace(FinishedGoodsInboundSnapshotValidator.ValidateStructural(allowed)), "identical box alternate is not duplicated");

            FinishedGoodsInboundSnapshot conflict = InboundSnapshot();
            conflict.Boxes[0].AlternateBoxCode = "PACK1";
            Equal(false, string.IsNullOrWhiteSpace(FinishedGoodsInboundSnapshotValidator.ValidateStructural(conflict)), "alternate conflicts with pack barcode");
            Pass();
        }

        private static void FinishedGoodsInboundStructuralValidationGuardsPalletBarcodeNamespace()
        {
            FinishedGoodsInboundSnapshot palletVsAlternate = InboundSnapshot();
            palletVsAlternate.Pallet = new FinishedGoodsInboundPalletSnapshot { SourceEntityId = "PALLET1", PalletCode = "PALLET-CODE" };
            palletVsAlternate.Boxes[0].AlternateBoxCode = "PALLET-CODE";
            Equal(false, string.IsNullOrWhiteSpace(FinishedGoodsInboundSnapshotValidator.ValidateStructural(palletVsAlternate)), "pallet code conflicts with alternate identifier");

            FinishedGoodsInboundSnapshot alternateVsPallet = InboundSnapshot();
            alternateVsPallet.Boxes[0].AlternateBoxCode = "PALLET-CODE";
            alternateVsPallet.Pallet = new FinishedGoodsInboundPalletSnapshot { SourceEntityId = "PALLET1", PalletCode = "pallet-code" };
            Equal(false, string.IsNullOrWhiteSpace(FinishedGoodsInboundSnapshotValidator.ValidateStructural(alternateVsPallet)), "alternate identifier conflicts with pallet code");
            Pass();
        }

        private static void FinishedGoodsInboundWeekCodeResolverSupportsAliasesAndYww()
        {
            WeekCodeResolver resolver = new WeekCodeResolver(new UnavailableWeekCodeRuleProvider());
            OperationResult<WeekCodeSnapshot> alias = resolver.Resolve("27-26", "WW-YY", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> yyww = resolver.Resolve("2627", "YYWW", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> wwyy = resolver.Resolve("2726", "WWYY", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> yyDashWw = resolver.Resolve("26-27", "YY-WW", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> wwSlashYy = resolver.Resolve("27/26", "WW/YY", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> wwDotYy = resolver.Resolve("27.26", "WW.YY", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> yww = resolver.Resolve("627", "YWW", new DateTime(2026, 8, 7));
            OperationResult<WeekCodeSnapshot> missingAnchor = resolver.Resolve("627", "YWW", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> unsupported = resolver.Resolve("2627", "YYMM", (DateTime?)null);
            OperationResult<WeekCodeSnapshot> missing = resolver.Resolve("2627", " ", (DateTime?)null);
            Equal(true, alias.IsSuccess, "inbound alias resolved");
            Equal(202627, alias.Data.WeekSortKey, "inbound alias sort key");
            Equal(true, yyww.IsSuccess, "inbound YYWW resolved");
            Equal(true, wwyy.IsSuccess, "inbound WWYY resolved");
            Equal(true, yyDashWw.IsSuccess, "inbound YY-WW resolved");
            Equal(true, wwSlashYy.IsSuccess, "inbound WW/YY resolved");
            Equal(true, wwDotYy.IsSuccess, "inbound WW.YY resolved");
            Equal(true, yww.IsSuccess, "inbound YWW resolved");
            Equal(202627, yww.Data.WeekSortKey, "inbound YWW sort key");
            Equal(false, missingAnchor.IsSuccess, "inbound YWW anchor required");
            Equal(false, unsupported.IsSuccess, "inbound unsupported rejected");
            Equal(false, missing.IsSuccess, "inbound missing rule rejected");
            Pass();
        }

        private static void FinishedGoodsInboundHasherIsCanonicalAndVersioned()
        {
            FinishedGoodsInboundSnapshot first = InboundSnapshot();
            FinishedGoodsInboundSnapshot second = InboundSnapshot();
            second.Boxes[0].InnerPacks[0].Qty = 1.0000m;
            Equal(WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(first), WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(second), "inbound decimal canonical hash");
            second.Boxes[0].InnerPacks.Add(new FinishedGoodsInboundPackSnapshot { SourceEntityId = "P2", PackCode = "PACK2", Qty = 1m, DateCode = "2627" });
            Equal(false, WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(first) == WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(second), "inbound collection boundaries include count");
            FinishedGoodsInboundSnapshot nullDateCode = InboundSnapshot();
            FinishedGoodsInboundSnapshot emptyDateCode = InboundSnapshot();
            FinishedGoodsInboundSnapshot nullTextDateCode = InboundSnapshot();
            FinishedGoodsInboundSnapshot whitespaceDateCode = InboundSnapshot();
            nullDateCode.Boxes[0].InnerPacks[0].DateCode = null;
            emptyDateCode.Boxes[0].InnerPacks[0].DateCode = string.Empty;
            nullTextDateCode.Boxes[0].InnerPacks[0].DateCode = "NULL";
            whitespaceDateCode.Boxes[0].InnerPacks[0].DateCode = "   ";
            FinishedGoodsInboundSnapshot stringZero = InboundSnapshot();
            FinishedGoodsInboundSnapshot numericZero = InboundSnapshot();
            stringZero.Boxes[0].InnerPacks[0].DateCode = "0";
            numericZero.Boxes[0].InnerPacks[0].Qty = 0m;
            Equal(false, WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(nullDateCode) == WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(emptyDateCode), "inbound null and empty datecode differ");
            Equal(false, WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(nullDateCode) == WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(nullTextDateCode), "inbound null and NULL text differ");
            Equal(false, WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(emptyDateCode) == WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(whitespaceDateCode), "inbound empty and whitespace datecode differ");
            Equal(false, WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(stringZero) == WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateHash(numericZero), "inbound string and decimal zero differ");
            Equal("FGI_V1", WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.HashVersion, "inbound hash version");
            Pass();
        }

        private static void FinishedGoodsInboundServiceReturnsDuplicateAndConflict()
        {
            FakeFinishedGoodsInboundRepository repository = new FakeFinishedGoodsInboundRepository();
            FinishedGoodsInboundService service = new FinishedGoodsInboundService(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            OperationResult<FinishedGoodsInboundResultDto> success = service.Receive(InboundRequest(InboundSnapshot(), "REQ1"));
            Equal(true, success.IsSuccess, "inbound service success");
            Equal("FGI_V1", repository.Command.HashVersion, "inbound command hash version");
            repository.Next = new FinishedGoodsInboundPersistenceResult { IsConflict = true, Message = "CONFLICT" };
            OperationResult<FinishedGoodsInboundResultDto> conflict = service.Receive(InboundRequest(InboundSnapshot(), null));
            Equal(false, conflict.IsSuccess, "inbound service conflict");
            Pass();
        }

        private static void FinishedGoodsInboundServicePropagatesPersistenceFailure()
        {
            FakeFinishedGoodsInboundRepository repository = new FakeFinishedGoodsInboundRepository { ThrowOnPersist = true };
            FinishedGoodsInboundService service = new FinishedGoodsInboundService(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            bool threw = false;
            try
            {
                service.Receive(InboundRequest(InboundSnapshot(), null));
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Equal(true, threw, "inbound persistence failure propagated");
            Pass();
        }

        private static void FinishedGoodsInboundDuplicateSkipsWeekResolution()
        {
            FakeFinishedGoodsInboundRepository repository = new FakeFinishedGoodsInboundRepository
            {
                Existing = new FinishedGoodsInboundPersistenceResult
                {
                    Data = new FinishedGoodsInboundResultDto { ReceiptId = 1, IsDuplicate = true },
                    Message = "DUPLICATE"
                }
            };
            FinishedGoodsInboundService service = new FinishedGoodsInboundService(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            FinishedGoodsInboundSnapshot snapshot = InboundSnapshot();
            snapshot.Boxes[0].WeekRuleValue = null;
            OperationResult<FinishedGoodsInboundResultDto> result = service.Receive(InboundRequest(snapshot, null));
            Equal(true, result.IsSuccess, "duplicate returned before week resolution");
            Equal(false, snapshot.Boxes[0].InnerPacks[0].WeekResolutionSucceeded, "duplicate did not resolve week");
            Equal(null, repository.Command, "duplicate did not persist");
            Pass();
        }

        private static void FinishedGoodsInboundExceptionIsPreparedAndPersisted()
        {
            FakeFinishedGoodsInboundRepository repository = new FakeFinishedGoodsInboundRepository();
            FinishedGoodsInboundService service = new FinishedGoodsInboundService(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            FinishedGoodsInboundSnapshot snapshot = InboundSnapshot();
            snapshot.Boxes[0].WeekRuleValue = null;
            snapshot.Boxes[0].InnerPacks[0].DateCode = null;
            OperationResult<FinishedGoodsInboundResultDto> result = service.Receive(InboundRequest(snapshot, null));
            Equal(true, result.IsSuccess, "week exception inbound persists");
            Equal(false, repository.Command.Snapshot.Boxes[0].InnerPacks[0].WeekResolutionSucceeded, "week exception prepared");
            Equal(null, repository.Command.Snapshot.Boxes[0].InnerPacks[0].WeekCode, "week exception has null snapshot");
            Equal("TESTER", repository.Command.Operator, "week exception uses required actor");
            Pass();
        }

        private static void FinishedGoodsInboundMissingDateCodesAreExceptionInbound()
        {
            foreach (string dateCode in new[] { null, string.Empty, "   " })
            {
                FakeFinishedGoodsInboundRepository repository = new FakeFinishedGoodsInboundRepository();
                FinishedGoodsInboundService service = new FinishedGoodsInboundService(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
                FinishedGoodsInboundSnapshot snapshot = InboundSnapshot();
                snapshot.Boxes[0].InnerPacks[0].DateCode = dateCode;
                OperationResult<FinishedGoodsInboundResultDto> result = service.Receive(InboundRequest(snapshot, null));
                Equal(true, result.IsSuccess, "missing datecode is exception inbound");
                Equal(false, repository.Command.Snapshot.Boxes[0].InnerPacks[0].WeekResolutionSucceeded, "missing datecode remains unresolved");
                Equal(null, repository.Command.Snapshot.Boxes[0].InnerPacks[0].WeekCode, "missing datecode has null week fields");
                Equal(dateCode, repository.Command.Snapshot.Boxes[0].InnerPacks[0].DateCode, "missing datecode raw source fact preserved");
            }
            Pass();
        }

        private static void FinishedGoodsInboundRequiresActor()
        {
            FakeFinishedGoodsInboundRepository repository = new FakeFinishedGoodsInboundRepository();
            FinishedGoodsInboundService service = new FinishedGoodsInboundService(repository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            OperationResult<FinishedGoodsInboundResultDto> result = service.Receive(new FinishedGoodsInboundReceiveRequest { Snapshot = InboundSnapshot() });
            Equal(false, result.IsSuccess, "inbound actor is required before persistence");
            Equal(null, repository.Command, "missing actor does not persist");
            Pass();
        }

        private static void FinishedGoodsInboundHashAndMoveIdsAreStable()
        {
            FinishedGoodsInboundSnapshot first = InboundSnapshot();
            FinishedGoodsInboundSnapshot second = InboundSnapshot();
            string firstId = WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateMoveRequestId(first, "OUTER_BOX", "B1");
            string secondId = WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateMoveRequestId(second, "OUTER_BOX", "B1");
            string otherId = WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateMoveRequestId(second, "OUTER_BOX", "B2");
            string firstMoveNo = WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateMoveNo(first, "OUTER_BOX", "B1");
            first.SourceSystem = "A"; first.SourceType = "B|C"; first.SourceEntityId = "D";
            second.SourceSystem = "A|B"; second.SourceType = "C"; second.SourceEntityId = "D";
            string separatedFirst = WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateMoveRequestId(first, "OUTER_BOX", "B1");
            string separatedSecond = WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.CreateMoveRequestId(second, "OUTER_BOX", "B1");
            Equal(firstId, secondId, "stable move request id");
            Equal(false, firstId == otherId, "root-specific move request id");
            Equal(false, separatedFirst == separatedSecond, "separator-safe move request id");
            Equal(true, firstId.Length <= 100, "move request id schema length");
            Equal(true, firstMoveNo.Length <= 50, "move number schema length");
            Equal(true, firstId.StartsWith(firstMoveNo), "move number derives from stable request id");
            Pass();
        }

        private static FinishedGoodsInboundSnapshot InboundSnapshot()
        {
            return new FinishedGoodsInboundSnapshot
            {
                SourceSystem = "MES", SourceType = "PLAN_STOCK", SourceEntityId = "S1", PlantCode = "P1",
                Boxes = new List<FinishedGoodsInboundBoxSnapshot>
                {
                    new FinishedGoodsInboundBoxSnapshot
                    {
                        SourceEntityId = "B1", BoxCode = "BOX1", MaterialCode = "M1", MaterialRevision = "R1", Uom = "PCS", WeekRuleSource = "TEST", WeekRuleValue = "YYWW",
                        InnerPacks = new List<FinishedGoodsInboundPackSnapshot>
                        {
                            new FinishedGoodsInboundPackSnapshot { SourceEntityId = "P1", PackCode = "PACK1", Qty = 1m, DateCode = "2627" }
                        }
                    }
                }
            };
        }

        private static FinishedGoodsInboundReceiveRequest InboundRequest(FinishedGoodsInboundSnapshot snapshot, string requestId)
        {
            return new FinishedGoodsInboundReceiveRequest { Snapshot = snapshot, RequestId = requestId, Operator = "TESTER" };
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

        private class FakeFinishedGoodsInboundRepository : IFinishedGoodsInboundRepository
        {
            public FinishedGoodsInboundCommand Command { get; private set; }
            public FinishedGoodsInboundPersistenceResult Next { get; set; }
            public FinishedGoodsInboundPersistenceResult Existing { get; set; }
            public bool ThrowOnPersist { get; set; }

            public FinishedGoodsInboundPersistenceResult FindExisting(FinishedGoodsInboundCommand command)
            {
                return Existing;
            }

            public FinishedGoodsInboundPersistenceResult Persist(FinishedGoodsInboundCommand command)
            {
                Command = command;
                if (ThrowOnPersist) throw new InvalidOperationException("simulated persistence failure");
                return Next ?? new FinishedGoodsInboundPersistenceResult
                {
                    Data = new FinishedGoodsInboundResultDto
                    {
                        ReceiptId = 1,
                        SnapshotHash = command.SnapshotHash,
                        HashVersion = command.HashVersion
                    },
                    Message = "SUCCESS"
                };
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

        private static void MesFinishedGoodsInboundSnapshotBuilderValidatesAndMaps()
        {
            MesFinishedGoodsInboundSnapshotBuilder builder = new MesFinishedGoodsInboundSnapshotBuilder();
            MesFinishedGoodsInboundSourceGraph valid = MesGraph(12m, new[] { 12m }, "BOX-A", "CUST-A", "2632");
            OperationResult<FinishedGoodsInboundReceiveRequest> result = builder.Build(valid);
            Equal(true, result.IsSuccess, "MES single pack valid");
            Equal("MES-FGI-730C7071-1EC9-42F2-8464-81EA6EC2D95B", result.Data.RequestId, "MES request id");
            Equal("PLT-99214", result.Data.Snapshot.Pallet.PalletCode, "MES pallet code");
            Equal("CUST-A", result.Data.Snapshot.Boxes[0].AlternateBoxCode, "MES alternate code");
            Equal("2632", result.Data.Snapshot.Boxes[0].InnerPacks[0].DateCode, "MES raw datecode");

            MesFinishedGoodsInboundSourceGraph sameCode = MesGraph(12m, new[] { 12m }, "BOX-A", " box-a ", "3226");
            Equal(null, builder.Build(sameCode).Data.Snapshot.Boxes[0].AlternateBoxCode, "MES duplicate alternate omitted");
            MesFinishedGoodsInboundSourceGraph multiPack = MesGraph(120m, new[] { 40m, 40m, 40m }, "BOX-A", null, "3226");
            Equal(true, builder.Build(multiPack).IsSuccess, "MES multi pack valid");
            MesFinishedGoodsInboundSourceGraph mismatchQty = MesGraph(120m, new[] { 40m, 40m, 39m }, "BOX-A", null, "2632");
            Equal(false, builder.Build(mismatchQty).IsSuccess, "MES quantity mismatch rejected");
            MesFinishedGoodsInboundSourceGraph mismatchMaterial = MesGraph(12m, new[] { 12m }, "BOX-A", null, "2632");
            mismatchMaterial.Packs[0].ProdNo = "OTHER";
            Equal(false, builder.Build(mismatchMaterial).IsSuccess, "MES material mismatch rejected");
            MesFinishedGoodsInboundSourceGraph multiBox = MesGraph(12m, new[] { 12m }, "BOX-A", null, "2632");
            MesBoxReadModel second = new MesBoxReadModel { PK_Box_Id = "BOX-GUID-B", PK_PlanStock_Id = "730C7071-1EC9-42F2-8464-81EA6EC2D95B", BoxID = "BOXID-B", BoxBarCode = "BOX-B", ProdNo = "MAT-B", ProdRev = "R2", UnitQty = 8m, PlantCode = "1010" };
            multiBox.Boxes.Add(second);
            multiBox.Packs.Add(new MesPackmasterReadModel { PK_Packmaster_Id = "PACK-GUID-B", BoxID = "BOXID-B", CustPackID = "PACK-B", PCSQty = 8m, DateCode = "3226", Createdt = new DateTime(2026, 8, 19), ProdNo = "MAT-B", ProdRev = "R2", PlantCode = "1010" });
            Equal(true, builder.Build(multiBox).IsSuccess, "MES mixed material pallet valid");
            Pass();
        }

        private static void MesWeekCodeRuleProviderUsesDistinctValues()
        {
            MesWeekCodeRuleProvider provider = new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = new List<string> { " WWYY ", "WWYY" } });
            MesWeekCodeRuleLookupResult same = provider.Resolve("1020", "14C89A259B", "32");
            Equal(true, same.IsSuccess, "same MES rules allowed"); Equal(2, same.RawRowCount, "same MES raw count"); Equal(1, same.DistinctRuleCount, "same MES distinct count"); Equal("WWYY", same.RuleValue, "same MES rule value");
            MesWeekCodeRuleLookupResult ambiguous = new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = new List<string> { "WWYY", "YYWW" } }).Resolve("1020", "14C89A259B", "32");
            Equal("WEEK_RULE_AMBIGUOUS", ambiguous.ErrorCode, "MES ambiguous rule");
            MesWeekCodeRuleLookupResult missing = new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = new List<string>() }).Resolve("1020", "14C89A259B", "32");
            Equal("WEEK_RULE_NOT_FOUND", missing.ErrorCode, "MES missing rule");
            Pass();
        }

        private static void MesWeekCodePreviewContinuesAfterErrors()
        {
            MesFinishedGoodsInboundSourceGraph source = MesGraph(24m, new[] { 12m, 12m }, "BOX-A", null, "3226");
            source.Packs[1].DateCode = "2627";
            FakeMesWeekCodeRuleRepository rules = new FakeMesWeekCodeRuleRepository { Values = new List<string> { "WWYY" } };
            MesFinishedGoodsInboundWeekCodePreviewService service = new MesFinishedGoodsInboundWeekCodePreviewService(new MesFinishedGoodsInboundSnapshotBuilder(), new MesWeekCodeRuleProvider(rules), new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            OperationResult<MesFinishedGoodsInboundWeekCodePreviewResult> preview = service.Preview(source);
            Equal(true, preview.IsSuccess, "MES preview builds"); Equal(2, preview.Data.ResolvedPackCount, "MES preview resolved packs"); Equal(202632, preview.Data.Packs[0].WeekSortKey.Value, "MES preview WWYY");
            MesFinishedGoodsInboundWeekCodePreviewService missing = new MesFinishedGoodsInboundWeekCodePreviewService(new MesFinishedGoodsInboundSnapshotBuilder(), new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = new List<string>() }), new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            OperationResult<MesFinishedGoodsInboundWeekCodePreviewResult> failed = missing.Preview(source);
            Equal(2, failed.Data.UnresolvedPackCount, "MES preview continues missing rules"); Equal(2, failed.Data.RuleNotFoundCount, "MES preview missing rule count"); Pass();
        }

        private static void MesFinishedGoodsInboundReceiveRequestPreparerUsesBoxSourceIdentity()
        {
            MesFinishedGoodsInboundSourceGraph source = MesGraph(24m, new[] { 12m, 12m }, "BOX-A", null, "3226");
            MesFinishedGoodsInboundWeekCodePreviewService previewService = new MesFinishedGoodsInboundWeekCodePreviewService(new MesFinishedGoodsInboundSnapshotBuilder(), new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = new List<string> { "WWYY" } }), new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()));
            OperationResult<MesFinishedGoodsInboundWeekCodePreviewResult> preview = previewService.Preview(source);
            MesFinishedGoodsInboundReceiveRequestPreparer preparer = new MesFinishedGoodsInboundReceiveRequestPreparer();
            OperationResult<FinishedGoodsInboundReceiveRequest> prepared = preparer.Prepare(preview.Data);
            Equal(true, prepared.IsSuccess, "MES prepared request succeeds");
            Equal(true, ReferenceEquals(preview.Data.Request, prepared.Data), "MES preparer preserves builder request");
            Equal("BOX-GUID-A", preview.Data.Packs[0].BoxSourceEntityId, "MES preview box source identity");
            Equal("WWYY", prepared.Data.Snapshot.Boxes[0].WeekRuleValue, "MES prepared rule value");
            preview.Data.Packs[1].RuleValue = "YYWW";
            Equal(false, preparer.Prepare(preview.Data).IsSuccess, "MES distinct box rules rejected");
            preview.Data.Packs[1].RuleValue = "WWYY";
            preview.Data.Packs[1].Status = "UNRESOLVED";
            preview.Data.UnresolvedPackCount = 1;
            Equal(false, preparer.Prepare(preview.Data).IsSuccess, "MES unresolved preview rejected");
            Pass();
        }

        private static void MesProductionOrchestrationUsesReceiveAndHoldPolicy()
        {
            FakeMesInboundService resolvedInbound = new FakeMesInboundService();
            OperationResult<MesFinishedGoodsInboundTriggerResult> resolved = MesTrigger(new List<string> { "YYWW", "YYWW" }, resolvedInbound).Trigger("730C7071-1EC9-42F2-8464-81EA6EC2D95B");
            Equal(true, resolved.IsSuccess, "MES trigger resolved success");
            Equal(1, resolvedInbound.ReceiveCount, "MES trigger resolved invokes core");
            Equal("MES_FLOW", resolvedInbound.Request.Snapshot.Boxes[0].WeekRuleSource, "MES trigger source injected");
            Equal("YYWW", resolvedInbound.Request.Snapshot.Boxes[0].WeekRuleValue, "MES trigger same multi-pack rule injected");

            FakeMesInboundService missingInbound = new FakeMesInboundService();
            OperationResult<MesFinishedGoodsInboundTriggerResult> missing = MesTrigger(new List<string>(), missingInbound).Trigger("730C7071-1EC9-42F2-8464-81EA6EC2D95B");
            Equal(true, missing.IsSuccess, "MES trigger missing rule continues to core");
            Equal(1, missingInbound.ReceiveCount, "MES trigger missing rule invokes core");
            Equal(null, missingInbound.Request.Snapshot.Boxes[0].WeekRuleValue, "MES trigger missing rule is not fabricated");

            FakeMesInboundService ambiguousInbound = new FakeMesInboundService();
            OperationResult<MesFinishedGoodsInboundTriggerResult> ambiguous = MesTrigger(new List<string> { "YYWW", "WWYY" }, ambiguousInbound).Trigger("730C7071-1EC9-42F2-8464-81EA6EC2D95B");
            Equal(true, ambiguous.IsSuccess, "MES trigger ambiguous rule continues to core");
            Equal(1, ambiguousInbound.ReceiveCount, "MES trigger ambiguous invokes core");
            Equal(null, ambiguousInbound.Request.Snapshot.Boxes[0].WeekRuleValue, "MES trigger ambiguous rule is not selected");

            FakeMesInboundService unsupportedInbound = new FakeMesInboundService();
            OperationResult<MesFinishedGoodsInboundTriggerResult> unsupported = MesTrigger(new List<string> { "YYMM" }, unsupportedInbound).Trigger("730C7071-1EC9-42F2-8464-81EA6EC2D95B");
            Equal(true, unsupported.IsSuccess, "MES trigger unsupported rule continues to core");
            Equal("YYMM", unsupportedInbound.Request.Snapshot.Boxes[0].WeekRuleValue, "MES trigger real unsupported rule has no fallback");

            FakeMesInboundService duplicateInbound = new FakeMesInboundService { Result = new FinishedGoodsInboundResultDto { ReceiptId = 9, IsDuplicate = true } };
            OperationResult<MesFinishedGoodsInboundTriggerResult> duplicate = MesTrigger(new List<string> { "YYWW" }, duplicateInbound).Trigger("730C7071-1EC9-42F2-8464-81EA6EC2D95B");
            Equal(true, duplicate.IsSuccess, "MES trigger duplicate success");
            Equal(true, duplicate.Data.IsDuplicate, "MES trigger duplicate mapped");
            Pass();
        }

        private static MesFinishedGoodsInboundOrchestrationService MesTrigger(List<string> values, FakeMesInboundService inbound)
        {
            return new MesFinishedGoodsInboundOrchestrationService(
                new FakeMesFinishedGoodsInboundRepository { Source = MesGraph(24m, new[] { 12m, 12m }, "BOX-A", null, "2620") },
                new MesFinishedGoodsInboundSnapshotBuilder(),
                new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = values }),
                inbound);
        }

        private class FakeMesWeekCodeRuleRepository : IMesWeekCodeRuleRepository
        {
            public List<string> Values { get; set; }
            public MesWeekCodeRuleReadResult Read(string plantCode, string prodNo, string prodRev) { return new MesWeekCodeRuleReadResult { RuleValues = Values }; }
        }

        private class FakeMesFinishedGoodsInboundRepository : IMesFinishedGoodsInboundRepository
        {
            public MesFinishedGoodsInboundSourceGraph Source { get; set; }
            public MesFinishedGoodsInboundSourceGraph GetPlanStock(string planStockSourceId) { return Source; }
        }

        private class FakeMesInboundService : IFinishedGoodsInboundService
        {
            public int ReceiveCount { get; private set; }
            public FinishedGoodsInboundReceiveRequest Request { get; private set; }
            public FinishedGoodsInboundResultDto Result { get; set; }
            public OperationResult<FinishedGoodsInboundResultDto> Receive(FinishedGoodsInboundReceiveRequest request)
            {
                ReceiveCount++;
                Request = request;
                return OperationResult<FinishedGoodsInboundResultDto>.Success(Result ?? new FinishedGoodsInboundResultDto { ReceiptId = 7, IsDuplicate = false });
            }
        }

        private static MesFinishedGoodsInboundSourceGraph MesGraph(decimal unitQty, decimal[] quantities, string boxCode, string customerCode, string dateCode)
        {
            MesFinishedGoodsInboundSourceGraph source = new MesFinishedGoodsInboundSourceGraph
            {
                PlanStock = new MesPlanStockReadModel { PK_PlanStock_Id = "730C7071-1EC9-42F2-8464-81EA6EC2D95B", Id = 99214, PlantCode = "1010", Stockby = "tester", Status = "入库", Stockdt = new DateTime(2026, 8, 19) },
                Boxes = new List<MesBoxReadModel> { new MesBoxReadModel { PK_Box_Id = "BOX-GUID-A", PK_PlanStock_Id = "730C7071-1EC9-42F2-8464-81EA6EC2D95B", BoxID = "BOXID-A", BoxBarCode = boxCode, CustboxID = customerCode, ProdNo = "MAT-A", ProdRev = "R1", UnitQty = unitQty, PlantCode = "1010" } }
            };
            for (int index = 0; index < quantities.Length; index++)
                source.Packs.Add(new MesPackmasterReadModel { PK_Packmaster_Id = "PACK-GUID-" + index, BoxID = "BOXID-A", CustPackID = "PACK-" + index, PCSQty = quantities[index], DateCode = dateCode, Createdt = new DateTime(2026, 8, 19), ProdNo = "MAT-A", ProdRev = "R1", PlantCode = "1010" });
            return source;
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
