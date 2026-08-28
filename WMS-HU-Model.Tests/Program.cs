using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using WMS_HU_Model.Infrastructure.Sap;
using WMS_HU_Model.Models.Audit;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Outbound.Integration;
using WMS_HU_Model.Models.Outbound.Formal;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Models.Inventory;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.MasterData;
using WMS_HU_Model.Models.Monitoring;
using WMS_HU_Model.Models.Putaway;
using WMS_HU_Model.Models.Relocation;
using WMS_HU_Model.Models.Repack;
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
                SapChargResolverMatchesLegacyRule();
                MesInboundSnapshotsAuthoritativeSapBatchNo();
                SapBatchPersistenceAndPickSnapshotContractsAlign();
                MesWeekCodeRuleProviderUsesDistinctValues();
                MesWeekCodePreviewContinuesAfterErrors();
                MesFinishedGoodsInboundReceiveRequestPreparerUsesBoxSourceIdentity();
                MesProductionOrchestrationUsesReceiveAndHoldPolicy();
                WeekCodeAuditClassifiesAndFiltersFacts();
                WeekCodeRecalculatePlansSuccessfulOnly();
                WeekCodeRecalculateCoversFailureAndBatchBoundaries();
                WeekCodeRecalculateEffectiveLocationSqlIsAligned();
                WeekCodeRecalculateReleaseRequestIdsAreScoped();
                FinishedGoodsInventoryQueryValidatesAndPages();
                FinishedGoodsInventorySqlPreservesCurrentInventorySemantics();
                EffectiveLocationResolverPreservesPublicLocationSemantics();
                InventoryLookupReturnsInheritedLocationInventory();
                InventoryLookupAggregatesAndRejectsConflictingInnerItems();
                InventoryLookupEnforcesResolutionAndWarehouseRules();
                WholePalletPutawayContextAndCommandAreAligned();
                PutawayBoxResolvesPalletInheritedMode();
                PutawayBoxResolvesDirectReceiveMode();
                PutawayBoxKeepsMergedChildrenBelowDirectReceiveBox();
                PutawayBoxRejectsInvalidDirectStates();
                PutawayBoxDirectReplayIsSafe();
                DirectReceivePutawayRepositoryIsAtomic();
                PalletPutawayRepositoryContractRemains();
                PutawayReplayAndChildInheritanceContractsAlign();
                PutawayRecommendationValidatesSourcesAndConflicts();
                PutawayRecommendationKeepsSingleLocationCluster();
                PutawayRecommendationKeepsExistingClusterInMixedLegacyLocation();
                PutawayRecommendationDynamicallyFillsAndExpands();
                PutawayRecommendationRanksMultiLocationAndTop3();
                PutawayRecommendationPrefersForwardExpansion();
                PutawayRecommendationUsesPairedRowsWithoutInventingLocations();
                PutawayRecommendationUsesWeekOnlyAsTieBreaker();
                PutawayRecommendationDoesNotGuessMissingTopology();
                PutawayRecommendationRepositoryIsReadOnlyAndCapacityFree();
                PutawayRecommendationDoesNotBlockManualOverride();
                BatchRelocationValidatesAtomicRequest();
                BatchRelocationHandlesReplayAndWarehouseValidationFailures();
                OutboundPickingStateResumesWithoutWrites();
                WholeBoxUnpackUsesAtomicCommandContract();
                PdaClosureRepositoriesAreAtomicAndTyped();
                CanonicalBoxCodeFormatsPlants();
                CanonicalBoxCodeScopesAndResetsSequence();
                CanonicalBoxCodeRejectsInvalidInputs();
                MergeBoxRulesAllowCrossLocationInventory();
                MergeBoxRulesRejectInvalidInventory();
                MergeBoxRulesRejectDimensionMismatch();
                MergeBoxServiceNormalizesAndPersists();
                MergeBoxServiceReplaysIdempotently();
                MergeBoxRepositoryIsAtomicAndConcurrentSafe();
                MergeBoxPublicContractKeepsServerOwnedFieldsOutOfRequest();
                HuDetailElevatesRootAndPreservesLocationSemantics();
                HuDetailSupportsShippedAndUnknownHu();
                HuDetailHistoryIsBoundedAndOrdered();
                FinishedGoodsInboundReadQueryValidatesAndNormalizes();
                FinishedGoodsInboundReadSqlAggregatesByReceipt();
                FinishedGoodsInboundReadDetailUsesBatchLocations();
                MasterDataWarehouseLifecycleIsGuarded();
                MasterDataWarehousePlantLifecycleIsGuarded();
                MasterDataLocationLifecycleIsGuarded();
                MasterDataLocationControlledMaintenanceIsEnforced();
                MasterDataSqlContractsAreSetBasedAndTyped();
                AuditOperationQueryIsBoundedAndNormalized();
                AuditMoveQueryPreservesMovementFacts();
                AuditHoldQueryCoversActiveAndReleased();
                AuditRequestTraceMergesPersistedFactsChronologically();
                AuditCapabilitiesDistinguishUnavailableFromZero();
                AuditSqlIsPagedParameterizedAndReadOnly();
                MonitoringHealthyOverviewUsesProvableMetrics();
                MonitoringWmsDbFailureIsDegraded();
                MonitoringMesDbFailureIsDegraded();
                MonitoringActivityFillsMissingDaysAndUsesReceipts();
                MonitoringContractIsReadOnlyAndSanitized();
                SapPostingQuanFieldsSerializeAsJsonNumbers();
                SapPostingCharFieldsRemainStrings();
                SapPostingLfimgBoxUsesCurrentLineQuantity();
                SapPostingS0089ConfigurationIsEndpointOnly();
                SapPostingConfigurationFailurePreventsClaim();
                SapPostingSendsAllRowsInOneCall();
                SapPostingRequiresSapBatchWithoutFallback();
                SapPostingUsesLineStorageLocation();
                SapPostingV1DefaultsAreStable();
                SapPostingSuccessShipsWithSapSource();
                SapPostingExplicitFailureKeepsPicked();
                SapPostingUnknownRequiresItReview();
                SapPostingRetryUsesFrozenPayload();
                SapPostingManualPostedUsesNoSapCall();
                SapPostingManualNotPostedBecomesRetryable();
                SapPostingLegacyShipAliasClosesBypass();
                SapDeliveryReceiveAddsGenericAudit();
                SapPostingMigrationAndRepositoryContractsAreSafe();
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
            Equal("FGI_V1", WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.GetHashVersion(first), "legacy inbound hash version preserved without CHARG facts");
            Equal("FGI_V2", WMS_HU_Model.Infrastructure.Inbound.FinishedGoodsInboundSnapshotHasher.HashVersion, "SAP CHARG inbound hash version");
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
            multiBox.Packs.Add(new MesPackmasterReadModel { PK_Packmaster_Id = "PACK-GUID-B", PackID = "572425", BoxID = "BOXID-B", CustPackID = "PACK-B", PCSQty = 8m, DateCode = "3226", Createdt = new DateTime(2026, 8, 19), ProdNo = "MAT-B", ProdRev = "R2", PlantCode = "1010" });
            Equal(true, builder.Build(multiBox).IsSuccess, "MES mixed material pallet valid");
            Pass();
        }

        private static void SapChargResolverMatchesLegacyRule()
        {
            SapChargResolver resolver = new SapChargResolver();
            Equal("A000472425", resolver.Resolve("1010", "472425", false), "1010 normal CHARG");
            Equal("B000472425", resolver.Resolve("1020", "472425", false), "1020 normal CHARG");
            Equal("AY00472425", resolver.Resolve("1010", "472425", true), "1010 special CHARG");
            Equal("BY00472425", resolver.Resolve("1020", "472425", true), "1020 special CHARG");
            Equal("A123456789", resolver.Resolve("1010", "99123456789", false), "normal SQL RIGHT semantics");
            Equal("AY23456789", resolver.Resolve("1010", "99123456789", true), "special SQL RIGHT semantics");
            string mesRepository = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "MesFinishedGoodsInboundRepository.cs"));
            Equal(true, mesRepository.Contains("EXISTS") && mesRepository.Contains("PK_PackLot") && mesRepository.Contains("PP_LotCard"), "any source lot special query");
            foreach (string orderType in new[] { "ZP02", "ZP05", "ZP06", "ZP08" })
                Equal(true, mesRepository.Contains("'" + orderType + "'"), orderType + " special source classification");
            Equal(false, mesRepository.Contains("'ZP01'"), "other order type remains normal");
            Pass();
        }

        private static void MesInboundSnapshotsAuthoritativeSapBatchNo()
        {
            MesFinishedGoodsInboundSourceGraph normalSource = MesGraph(12m, new[] { 12m }, "BOX-A", null, "2632");
            normalSource.Packs[0].PackID = "472425";
            FinishedGoodsInboundReceiveRequest normalRequest = new MesFinishedGoodsInboundSnapshotBuilder().Build(normalSource).Data;
            normalRequest.Snapshot.Boxes[0].InnerPacks[0].SapBatchNo = "CLIENT-FABRICATED";
            FakeFinishedGoodsInboundRepository normalRepository = new FakeFinishedGoodsInboundRepository();
            OperationResult<FinishedGoodsInboundResultDto> normal = new FinishedGoodsInboundService(normalRepository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider())).Receive(normalRequest);
            Equal(true, normal.IsSuccess, "normal MES CHARG inbound succeeds");
            FinishedGoodsInboundPackSnapshot normalPack = normalRepository.Command.Snapshot.Boxes[0].InnerPacks[0];
            Equal("472425", normalPack.PackId, "authoritative PackID snapshot");
            Equal("A000472425", normalPack.SapBatchNo, "normal SAPBatchNo snapshot");
            Equal(false, normalPack.SapBatchNo == "CLIENT-FABRICATED", "client SAPBatchNo is not authoritative");
            Equal("FGI_V2", normalRepository.Command.HashVersion, "CHARG facts use V2 hash");

            MesFinishedGoodsInboundSourceGraph specialSource = MesGraph(12m, new[] { 12m }, "BOX-A", null, "2632");
            specialSource.Packs[0].PackID = "472425";
            specialSource.Packs[0].IsSpecialSapCharg = true;
            FakeFinishedGoodsInboundRepository specialRepository = new FakeFinishedGoodsInboundRepository();
            new FinishedGoodsInboundService(specialRepository, new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()))
                .Receive(new MesFinishedGoodsInboundSnapshotBuilder().Build(specialSource).Data);
            Equal("AY00472425", specialRepository.Command.Snapshot.Boxes[0].InnerPacks[0].SapBatchNo, "special SAPBatchNo snapshot");
            Pass();
        }

        private static void SapBatchPersistenceAndPickSnapshotContractsAlign()
        {
            string inbound = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "FinishedGoodsInboundRepository.cs"));
            string pick = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "FormalOutboundRepository.cs"));
            string posting = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "SapOutboundPostingRepository.cs"));
            Equal(true, inbound.Contains("'MES_PACK_ID'") && inbound.Contains("SAPBatchNo") && inbound.Contains("pack.SapBatchNo"), "PackID identifier and HU Item SAPBatchNo persisted");
            Equal(true, pick.Contains("item.SAPBatchNo") && pick.Contains("allocation.Item.SAPBatchNo"), "formal pick snapshots exact SAPBatchNo");
            Equal(true, posting.Contains("pdi.SAPBatchNo") && posting.Contains("CHARG = first.SAPBatchNo"), "S0089 uses pick snapshot SAPBatchNo");
            Equal(false, posting.Contains("pdi.BatchNo,pdi.AllocatedQty"), "no internal BatchNo fallback");
            Equal(true, posting.Contains("SAP_POSTING_CHARG_MISSING"), "unresolved SAPBatchNo remains blocked");
            Equal(true, posting.Contains("LFIMG_BOX = lineQuantity"), "LFIMG_BOX remains row batch quantity");
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

        private static void WeekCodeAuditClassifiesAndFiltersFacts()
        {
            List<WeekCodeAuditFact> facts = new List<WeekCodeAuditFact>
            {
                AuditFact(1, "2631", null, null, null, "WEEK_RULE_MISSING: missing", "YYWW"),
                AuditFact(2, "2631", null, null, null, "UNSUPPORTED_WEEK_RULE: unsupported", "见备注"),
                AuditFact(3, "BAD", null, null, null, "INVALID_DATECODE: bad", "YYWW"),
                AuditFact(4, "2631", new DateTime(2026, 8, 19), 2031, 26, null, "WWYY"),
                AuditFact(5, "2632", new DateTime(2026, 8, 19), 2032, 26, null, "WWYY"),
                AuditFact(6, "2620", new DateTime(2026, 8, 19), 2026, 20, null, "YYWW"),
                AuditFact(7, "2701", new DateTime(2026, 12, 30), 2027, 1, null, "YYWW"),
                AuditFact(8, "2630", null, null, null, "WEEK_RULE_MISSING: missing", "YYWW"),
                AuditFact(8, "2631", null, null, null, "WEEK_RULE_MISSING: missing", "YYWW")
            };
            facts[3].MaterialCode = "18L160050G"; facts[3].MaterialRevision = "11";
            facts[4].MaterialCode = "20L030436H"; facts[4].MaterialRevision = "91";
            facts[3].PackSources.Add(new WeekCodeAuditPackSource { SourceSystem = "MES", SourceEntityId = "PACK-4-LATER" });
            FakeMesFinishedGoodsInboundRepository mes = new FakeMesFinishedGoodsInboundRepository
            {
                PackCreatedTimes = new Dictionary<string, DateTime?>
                {
                    { "PACK-4", new DateTime(2026, 8, 19) },
                    { "PACK-4-LATER", new DateTime(2026, 8, 20) },
                    { "PACK-5", new DateTime(2026, 8, 19) }
                }
            };
            WeekCodeAuditService service = new WeekCodeAuditService(
                new FakeWeekCodeAuditRepository { Facts = facts },
                mes);
            OperationResult<WeekCodeAuditResultDto> all = service.Query(new WeekCodeAuditQuery());
            Equal(true, all.IsSuccess, "audit query succeeds");
            Equal(7, all.Data.TotalCount, "audit preserves different datecode facts and omits normal/year-end facts");
            Equal("RULE_MISSING", all.Data.Items.First(x => x.BoxHUId == 1).AnomalyType, "audit rule missing");
            Equal("RULE_UNSUPPORTED", all.Data.Items.First(x => x.BoxHUId == 2).AnomalyType, "audit unsupported");
            Equal("INVALID_DATECODE", all.Data.Items.First(x => x.BoxHUId == 3).AnomalyType, "audit invalid datecode");
            WeekCodeAuditItemDto direction = all.Data.Items.First(x => x.BoxHUId == 4);
            Equal("SUSPICIOUS_RULE_DIRECTION", direction.AnomalyType, "audit rule direction");
            Equal("YYWW", direction.CandidateRuleValue, "audit candidate rule");
            Equal(202631, direction.CandidateWeekSortKey.Value, "audit candidate week");
            Equal(new DateTime(2026, 8, 19), direction.PackCreatedTime.Value, "audit group uses earliest MES created time");
            Equal(1, mes.PackCreatedTimeQueryCount, "audit MES created time is batched");
            Equal(false, all.Data.Items.Any(x => x.BoxHUId == 6 || x.BoxHUId == 7), "audit normal and year-end excluded");
            OperationResult<WeekCodeAuditResultDto> filtered = service.Query(new WeekCodeAuditQuery { AnomalyType = "RULE_MISSING", RiskLevel = "HIGH", Page = 1, PageSize = 1 });
            Equal(3, filtered.Data.TotalCount, "audit filters before paging"); Equal(1, filtered.Data.Items.Count, "audit page size");
            Equal(false, service.Query(new WeekCodeAuditQuery { Page = -1 }).IsSuccess, "audit page validation");
            Equal(false, service.Query(new WeekCodeAuditQuery { PageSize = 201 }).IsSuccess, "audit page size validation");
            Pass();
        }

        private static WeekCodeAuditFact AuditFact(long id, string dateCode, DateTime? packCreatedTime, int? weekYear, int? weekNo, string holdReason, string rule)
        {
            return new WeekCodeAuditFact
            {
                BoxHUId = id, BoxCode = "BOX-" + id, PlantCode = "1010", MaterialCode = "MAT", MaterialRevision = "R1", DateCode = dateCode,
                PackCreatedTime = packCreatedTime, WeekYear = weekYear, WeekNo = weekNo, WeekSortKey = weekYear.HasValue && weekNo.HasValue ? weekYear.Value * 100 + weekNo.Value : (int?)null,
                WeekRuleSource = "MES_FLOW", WeekRuleValue = rule, IsHold = holdReason != null, HoldReasonCode = holdReason == null ? null : "WEEKCODE_UNRESOLVED", HoldReason = holdReason,
                PackCount = 1, TotalQty = 1m,
                PackSources = new List<WeekCodeAuditPackSource> { new WeekCodeAuditPackSource { SourceSystem = "MES", SourceEntityId = "PACK-" + id } }
            };
        }

        private class FakeMesWeekCodeRuleRepository : IMesWeekCodeRuleRepository
        {
            public List<string> Values { get; set; }
            public MesWeekCodeRuleReadResult Read(string plantCode, string prodNo, string prodRev) { return new MesWeekCodeRuleReadResult { RuleValues = Values }; }
        }

        private class FakeMesFinishedGoodsInboundRepository : IMesFinishedGoodsInboundRepository
        {
            public MesFinishedGoodsInboundSourceGraph Source { get; set; }
            public Dictionary<string, DateTime?> PackCreatedTimes { get; set; }
            public MesFinishedGoodsInboundSourceGraph GetPlanStock(string planStockSourceId) { return Source; }
            public int PackCreatedTimeQueryCount { get; private set; }
            public Dictionary<string, DateTime?> GetPackCreatedTimes(IList<string> packSourceEntityIds) { PackCreatedTimeQueryCount++; return PackCreatedTimes ?? new Dictionary<string, DateTime?>(); }
        }

        private class FakeWeekCodeAuditRepository : IWeekCodeAuditRepository
        {
            public List<WeekCodeAuditFact> Facts { get; set; }
            public List<WeekCodeAuditFact> QueryFacts(WeekCodeAuditQuery query, int maxRows) { return Facts; }
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
                source.Packs.Add(new MesPackmasterReadModel { PK_Packmaster_Id = "PACK-GUID-" + index, PackID = (472425 + index).ToString(), BoxID = "BOXID-A", CustPackID = "PACK-" + index, PCSQty = quantities[index], DateCode = dateCode, Createdt = new DateTime(2026, 8, 19), ProdNo = "MAT-A", ProdRev = "R1", PlantCode = "1010" });
            return source;
        }

        private static void WeekCodeRecalculatePlansSuccessfulOnly()
        {
            FakeWeekCodeRecalculateRepository repo = new FakeWeekCodeRecalculateRepository();
            repo.Boxes.Add(new WeekCodeRecalculateBoxFact { BoxHUId = 1, BoxCode = "B1", IsHold = true, Items = new List<WeekCodeRecalculateItemFact> { new WeekCodeRecalculateItemFact { ItemId = 1, DateCode = "2631", OldRuleSource = "MES_FLOW", OldRuleValue = "WWYY", OldWeekFormatCode = "WWYY", OldWeekYear = 2031, OldWeekNo = 26, OldWeekSortKey = 203126 } } });
            WeekCodeRecalculateService service = new WeekCodeRecalculateService(repo, new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = new List<string> { "YYWW" } }), new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()), new FakeMesFinishedGoodsInboundRepository());
            OperationResult<WeekCodeRecalculateResultDto> updated = service.Recalculate(new WeekCodeRecalculateRequest { PlantCode = "1010", MaterialCode = "18L160050G", MaterialRevision = "11", Operator = "SYSTEM", RequestId = "R1" });
            Equal(202631, repo.LastPlan.Items[0].WeekCode.WeekSortKey, "recalculate corrected week"); Equal(1, updated.Data.UpdatedBoxCount, "recalculate updated box"); Equal("R1", updated.Data.RequestId, "recalculate response request id"); Equal((short?)2026, updated.Data.Items[0].ItemResults[0].NewWeekYear, "recalculate response after year");
            repo.Boxes[0].Items[0].OldRuleValue = "YYWW"; repo.Boxes[0].Items[0].OldRuleSource = "MES_FLOW"; repo.Boxes[0].Items[0].OldWeekFormatCode = "YYWW"; repo.Boxes[0].Items[0].OldWeekYear = 2026; repo.Boxes[0].Items[0].OldWeekNo = 31; repo.Boxes[0].Items[0].OldWeekSortKey = 202631; repo.ApplyCount = 0;
            OperationResult<WeekCodeRecalculateResultDto> unchanged = service.Recalculate(new WeekCodeRecalculateRequest { PlantCode = "1010", MaterialCode = "18L160050G", MaterialRevision = "11", Operator = "SYSTEM", RequestId = "R1" });
            Equal(1, unchanged.Data.UnchangedBoxCount, "recalculate unchanged"); Equal(0, repo.ApplyCount, "recalculate unchanged no write"); Pass();
        }

        private class FakeWeekCodeRecalculateRepository : IWeekCodeRecalculateRepository
        {
            public List<WeekCodeRecalculateBoxFact> Boxes = new List<WeekCodeRecalculateBoxFact>(); public WeekCodeRecalculateBoxPlan LastPlan; public int ApplyCount; public long? ThrowBoxId;
            public List<WeekCodeRecalculateBoxFact> GetCurrentBoxes(string plant, string material, string revision) { return Boxes; }
            public WeekCodeRecalculateBoxResultDto ApplyBox(WeekCodeRecalculateBoxPlan plan, WeekCodeRecalculateRequest request) { LastPlan = plan; ApplyCount++; if (ThrowBoxId == plan.BoxHUId) throw new InvalidOperationException("simulated box rollback"); return new WeekCodeRecalculateBoxResultDto { BoxHUId = plan.BoxHUId, BoxCode = plan.BoxCode, Result = "UPDATED", BeforeIsHold = plan.BeforeIsHold, AfterIsHold = false, ReleasedWeekCodeHold = plan.BeforeIsHold, UpdatedItemCount = plan.Items.Count, ItemResults = plan.Items.Select(WeekCodeRecalculateItemResult).ToList() }; }
            private static WeekCodeRecalculateItemResultDto WeekCodeRecalculateItemResult(WeekCodeRecalculateItemPlan item) { return new WeekCodeRecalculateItemResultDto { DateCode = item.DateCode, OldWeekRuleSource = item.OldRuleSource, OldWeekRuleValue = item.OldRuleValue, OldWeekFormatCode = item.OldWeekFormatCode, OldWeekYear = item.OldWeekYear, OldWeekNo = item.OldWeekNo, OldWeekSortKey = item.OldWeekSortKey, NewWeekRuleSource = item.WeekCode.WeekRuleSource, NewWeekRuleValue = item.WeekCode.WeekRuleValue, NewWeekFormatCode = item.WeekCode.WeekFormatCode, NewWeekYear = item.WeekCode.WeekYear, NewWeekNo = item.WeekCode.WeekNo, NewWeekSortKey = item.WeekCode.WeekSortKey, Result = "UPDATED" }; }
        }

        private static void WeekCodeRecalculateCoversFailureAndBatchBoundaries()
        {
            WeekCodeRecalculateRequest request = new WeekCodeRecalculateRequest { PlantCode = "1010", MaterialCode = "20L030436H", MaterialRevision = "91", Operator = "SYSTEM", RequestId = "R2" };
            FakeWeekCodeRecalculateRepository second = new FakeWeekCodeRecalculateRepository();
            second.Boxes.Add(RecalculateBox(2, "2632", "WWYY", 203226));
            OperationResult<WeekCodeRecalculateResultDto> corrected = RecalculateService(second, "YYWW").Recalculate(request);
            Equal(202632, second.LastPlan.Items[0].WeekCode.WeekSortKey, "recalculate second correction"); Equal(1, corrected.Data.UpdatedBoxCount, "recalculate second updated");

            FakeWeekCodeRecalculateRepository mixed = new FakeWeekCodeRecalculateRepository();
            WeekCodeRecalculateBoxFact mixedBox = RecalculateBox(3, "2631", null, null); mixedBox.Items.Add(new WeekCodeRecalculateItemFact { ItemId = 32, DateCode = "BAD", OldWeekSortKey = null }); mixed.Boxes.Add(mixedBox);
            OperationResult<WeekCodeRecalculateResultDto> mixedResult = RecalculateService(mixed, "YYWW").Recalculate(request);
            Equal(1, mixedResult.Data.FailedBoxCount, "recalculate mixed box fails atomically"); Equal(0, mixed.ApplyCount, "recalculate mixed box zero write");

            FakeWeekCodeRecalculateRepository failed = new FakeWeekCodeRecalculateRepository(); failed.Boxes.Add(RecalculateBox(4, "2631", "YYWW", 202631));
            OperationResult<WeekCodeRecalculateResultDto> failedResult = RecalculateService(failed, "见备注").Recalculate(request);
            Equal(1, failedResult.Data.FailedBoxCount, "recalculate unsupported fails"); Equal(0, failed.ApplyCount, "recalculate failed parse zero write"); Equal(202631, failed.Boxes[0].Items[0].OldWeekSortKey.Value, "recalculate preserves old week");

            FakeWeekCodeRecalculateRepository separate = new FakeWeekCodeRecalculateRepository { ThrowBoxId = 6 }; separate.Boxes.Add(RecalculateBox(5, "2631", "WWYY", 203126)); separate.Boxes.Add(RecalculateBox(6, "2632", "WWYY", 203226));
            OperationResult<WeekCodeRecalculateResultDto> partial = RecalculateService(separate, "YYWW").Recalculate(request);
            Equal(1, partial.Data.UpdatedBoxCount, "recalculate first box commits"); Equal(1, partial.Data.FailedBoxCount, "recalculate second box isolated"); Equal(2, separate.ApplyCount, "recalculate both boxes attempted");

            FakeWeekCodeRecalculateRepository empty = new FakeWeekCodeRecalculateRepository(); OperationResult<WeekCodeRecalculateResultDto> zero = RecalculateService(empty, "YYWW").Recalculate(request); Equal(0, zero.Data.BoxCount, "recalculate zero inventory");
            FakeWeekCodeRecalculateRepository noRule = new FakeWeekCodeRecalculateRepository(); noRule.Boxes.Add(RecalculateBox(7, "2631", null, null)); OperationResult<WeekCodeRecalculateResultDto> missing = RecalculateService(noRule, null).Recalculate(request); Equal(false, missing.IsSuccess, "recalculate missing MES rule"); Equal(0, noRule.ApplyCount, "recalculate missing rule zero write"); Pass();
        }

        private static WeekCodeRecalculateService RecalculateService(FakeWeekCodeRecalculateRepository repository, string rule)
        {
            return new WeekCodeRecalculateService(repository, new MesWeekCodeRuleProvider(new FakeMesWeekCodeRuleRepository { Values = rule == null ? new List<string>() : new List<string> { rule } }), new WeekCodeResolver(new UnavailableWeekCodeRuleProvider()), new FakeMesFinishedGoodsInboundRepository());
        }

        private static void WeekCodeRecalculateEffectiveLocationSqlIsAligned()
        {
            string[] sqlContracts =
            {
                WeekCodeRecalculateRepository.CurrentBoxesSql.ToUpperInvariant(),
                WeekCodeRecalculateRepository.RevalidateBoxSql.ToUpperInvariant()
            };

            foreach (string sql in sqlContracts)
            {
                Equal(true, sql.Contains("LEFT JOIN DBO.WMS_HU_CURRENTLOCATION DC"), "case B direct location available");
                Equal(true, sql.Contains("PR.CHILDHUID = B.ID AND PR.ISACTIVE = 1"), "case A/F active parent relation only");
                Equal(true, sql.Contains("PH.LIFECYCLESTATUS = 'ACTIVE'"), "case F inactive parent excluded");
                Equal(true, sql.Contains("COALESCE(DL.ID, PL.ID) IS NOT NULL"), "case E effective location required");
                Equal(true, sql.Contains("COALESCE(DL.LOCATIONCODE, PL.LOCATIONCODE) <> 'SHIPPED'"), "case C/D effective shipped excluded");
                Equal(false, sql.Contains("B.ISHOLD = 0"), "held inventory remains eligible");
                Equal(false, sql.Contains("WEEKSORTKEY IS NOT NULL"), "unresolved inventory remains eligible");
            }

            Pass();
        }

        private static void WeekCodeRecalculateReleaseRequestIdsAreScoped()
        {
            const string batchRequestId = "WEEK-RULE-CHANGE-08A700837B-02-001";
            string firstHold = WeekCodeRecalculateRepository.BuildHoldReleaseRequestId(batchRequestId, 101);
            string secondHold = WeekCodeRecalculateRepository.BuildHoldReleaseRequestId(batchRequestId, 102);

            Equal(false, string.Equals(firstHold, secondHold, StringComparison.Ordinal), "two holds use different child request ids");
            Equal(firstHold, WeekCodeRecalculateRepository.BuildHoldReleaseRequestId(batchRequestId, 101), "same hold retry uses deterministic child request id");
            Equal(batchRequestId + ":HOLD:101", firstHold, "normal child request id format");

            string longBatch = new string('R', 100);
            string longChild = WeekCodeRecalculateRepository.BuildHoldReleaseRequestId(longBatch, long.MaxValue);
            Equal(true, longChild.Length <= WeekCodeRecalculateRepository.HoldReleaseRequestIdMaxLength, "child request id fits varchar 100");
            Equal(longChild, WeekCodeRecalculateRepository.BuildHoldReleaseRequestId(longBatch, long.MaxValue), "hashed child request id is deterministic");
            Pass();
        }

        private static WeekCodeRecalculateBoxFact RecalculateBox(long id, string dateCode, string rule, int? sortKey)
        {
            return new WeekCodeRecalculateBoxFact { BoxHUId = id, BoxCode = "BOX-" + id, Items = new List<WeekCodeRecalculateItemFact> { new WeekCodeRecalculateItemFact { ItemId = id * 10 + 1, DateCode = dateCode, OldRuleSource = rule == null ? null : "MES_FLOW", OldRuleValue = rule, OldWeekFormatCode = rule, OldWeekYear = sortKey.HasValue ? (short?)(sortKey.Value / 100) : null, OldWeekNo = sortKey.HasValue ? (byte?)(sortKey.Value % 100) : null, OldWeekSortKey = sortKey } } };
        }

        private static void FinishedGoodsInventoryQueryValidatesAndPages()
        {
            FakeFinishedGoodsInventoryRepository repository = new FakeFinishedGoodsInventoryRepository();
            FinishedGoodsInventoryService service = new FinishedGoodsInventoryService(repository);
            OperationResult<FinishedGoodsInventoryResultDto> result = service.Query(new FinishedGoodsInventoryQuery
            {
                PlantCode = " 1010 ",
                MaterialCode = " MAT-A ",
                BoxCode = " BOX-1 ",
                IsHold = true
            });

            Equal(true, result.IsSuccess, "inventory query succeeds");
            Equal(1, repository.LastQuery.Page, "inventory default page");
            Equal(50, repository.LastQuery.PageSize, "inventory default page size");
            Equal("1010", repository.LastQuery.PlantCode, "inventory plant normalized");
            Equal("MAT-A", repository.LastQuery.MaterialCode, "inventory material normalized");
            Equal("BOX-1", repository.LastQuery.BoxCode, "inventory box normalized");
            Equal(true, repository.LastQuery.IsHold.Value, "inventory hold filter preserved");
            Equal(false, service.Query(new FinishedGoodsInventoryQuery { Page = -1 }).IsSuccess, "inventory rejects invalid page");
            Equal(false, service.Query(new FinishedGoodsInventoryQuery { PageSize = 201 }).IsSuccess, "inventory rejects oversized page");
            Equal(false, service.GetPreview("missing").IsSuccess, "inventory preview missing box");
            Pass();
        }

        private static void FinishedGoodsInventorySqlPreservesCurrentInventorySemantics()
        {
            string sql = FinishedGoodsInventoryRepository.CurrentInventoryFromWhereSql.ToUpperInvariant();
            Equal(true, sql.Contains("B.HUTYPE = 'OUTER_BOX'"), "inventory grain outer box");
            Equal(true, sql.Contains("B.LIFECYCLESTATUS = 'ACTIVE'"), "inventory active boxes only");
            Equal(true, sql.Contains("PACK.LIFECYCLESTATUS = 'ACTIVE'"), "inventory active packs only");
            Equal(true, sql.Contains("ITEM.ISACTIVE = 1"), "inventory active items only");
            Equal(true, sql.Contains("PR.CHILDHUID = B.ID AND PR.ISACTIVE = 1"), "inventory active parent relation only");
            Equal(true, sql.Contains("PH.LIFECYCLESTATUS = 'ACTIVE'"), "inventory inactive parent excluded");
            Equal(true, sql.Contains("COALESCE(DL.ID, PL.ID) IS NOT NULL"), "inventory effective location required");
            Equal(true, sql.Contains("COALESCE(DL.LOCATIONCODE, PL.LOCATIONCODE) <> 'SHIPPED'"), "inventory shipped excluded");
            Equal(true, FinishedGoodsInventoryRepository.QuantityAggregationSql.ToUpperInvariant().Contains("SUM(ITEM.QTY)"), "inventory quantity uses active item sum");
            Equal(true, sql.Contains("@PLANTCODE") && sql.Contains("@MATERIALCODE") && sql.Contains("@BOXCODE") && sql.Contains("@LOCATIONCODE") && sql.Contains("@ISHOLD"), "inventory filters are server side");
            Pass();
        }

        private class FakeFinishedGoodsInventoryRepository : IFinishedGoodsInventoryRepository
        {
            public FinishedGoodsInventoryQuery LastQuery { get; private set; }

            public FinishedGoodsInventoryResultDto Query(FinishedGoodsInventoryQuery query)
            {
                LastQuery = query;
                return new FinishedGoodsInventoryResultDto { Page = query.Page, PageSize = query.PageSize, TotalCount = 0 };
            }

            public FinishedGoodsInventoryPreviewDto GetPreview(string boxCode)
            {
                return null;
            }
        }

        private static void EffectiveLocationResolverPreservesPublicLocationSemantics()
        {
            FakeHuRepository huRepository = HuHierarchy();
            FakeEffectiveLocationResolver resolver = new FakeEffectiveLocationResolver();
            resolver.Locations[3] = new HuLocationDto
            {
                HuId = 3,
                HuCode = "PACK-1",
                DirectLocationCode = null,
                EffectiveLocationCode = "H201",
                EffectiveWarehouseCode = "FG01",
                LocationSourceHuCode = "PALLET-1",
                LocationSourceHuType = "PALLET",
                LocationSource = "PARENT_PALLET",
                IsInherited = true
            };
            HuLocationService service = new HuLocationService(
                huRepository,
                new FakeLocationMaintenanceRepository(),
                new FakeHuLocationRepository(),
                resolver);

            OperationResult<HuLocationDto> result = service.GetLocation(" pack-1 ");
            Equal(true, result.IsSuccess, "HU location resolver succeeds");
            Equal(null, result.Data.DirectLocationCode, "HU location direct field remains explicit");
            Equal("H201", result.Data.EffectiveLocationCode, "HU location effective field remains available");
            Equal("PALLET", result.Data.LocationSourceHuType, "HU location exposes source HU type");
            Equal("PARENT_PALLET", result.Data.LocationSource, "HU location exposes actual source level");
            Equal(true, EffectiveLocationResolver.ActiveParentPredicateSql.Contains("LifecycleStatus = 'ACTIVE'"), "effective resolver excludes inactive parents");
            string inventorySql = InventoryQueryRepository.LocationInventorySql.ToUpperInvariant();
            Equal(true, inventorySql.Contains("BOX.LIFECYCLESTATUS = 'ACTIVE'"), "PDA location inventory uses active outer boxes");
            Equal(true, inventorySql.Contains("RELATION.ISACTIVE = 1") && inventorySql.Contains("PACK.LIFECYCLESTATUS = 'ACTIVE'") && inventorySql.Contains("ITEM.ISACTIVE = 1"), "PDA location inventory uses active relations packs and items");
            Equal(false, inventorySql.Contains(" UPDATE ") || inventorySql.Contains(" INSERT ") || inventorySql.Contains(" DELETE ") || inventorySql.Contains(" MERGE "), "PDA location inventory SQL is read only");
            Pass();
        }

        private static void InventoryLookupReturnsInheritedLocationInventory()
        {
            FakeInventoryQueryRepository repository = new FakeInventoryQueryRepository();
            repository.Locations["FG01|H201"] = new InventoryLookupLocationRecord { Id = 8, WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = "H201" };
            repository.LocationFacts.Add(Fact(10, "BOX-2", 101, "PACK-2", "MAT-1", "01", "B-2", 2026, 35, 50m, true));
            repository.LocationFacts.Add(Fact(9, "BOX-1", 99, "PACK-1", "MAT-1", "01", "B-1", 2026, 34, 100m, false));
            repository.LocationFacts.Add(Fact(9, "BOX-1", 100, "PACK-3", "MAT-1", "01", "B-3", 2026, 35, 100m, false));
            FakeEffectiveLocationResolver resolver = new FakeEffectiveLocationResolver();
            resolver.HuIdsByLocation[8] = new List<long> { 9, 10 };
            InventoryQueryService service = new InventoryQueryService(repository, new FakeHuIdentifierService(), resolver);

            OperationResult<InventoryLookupDto> lookup = service.Lookup(" FG01 ", " H201 ");
            Equal(true, lookup.IsSuccess, "location lookup succeeds");
            InventoryLocationLookupDto result = (InventoryLocationLookupDto)lookup.Data;
            Equal("LOCATION", result.ResultType, "location lookup result type");
            Equal("BOX-1,BOX-2", string.Join(",", result.Items.Select(x => x.BoxCode)), "location boxes sorted deterministically");
            Equal("2634/2635", result.Items[0].WeekDisplay, "location weeks aggregate as YYWW");
            Equal(200m, result.Items[0].Qty, "location quantity sums active items");
            Equal(2, result.Items[0].PackCount, "location pack count is distinct");
            Equal(true, result.Items[1].IsHold, "held box remains visible");
            Equal("OUTER_BOX", resolver.LastFindHuType, "location inventory resolves effective outer boxes");

            repository.Locations["FG01|SHIPPED"] = new InventoryLookupLocationRecord { Id = 88, WarehouseCode = "FG01", ZoneCode = "SYSTEM", LocationCode = "SHIPPED" };
            InventoryLocationLookupDto shipped = (InventoryLocationLookupDto)service.Lookup("FG01", "SHIPPED").Data;
            Equal(0, shipped.Items.Count, "shipped location does not expose shelf inventory");
            Pass();
        }

        private static void InventoryLookupAggregatesAndRejectsConflictingInnerItems()
        {
            FakeInventoryQueryRepository repository = new FakeInventoryQueryRepository();
            repository.Hus[21] = new HuDto { Id = 21, HuCode = "PK-1", HuType = "INNER_PACK", LifecycleStatus = "ACTIVE", IsHold = false };
            repository.InnerFacts[21] = new List<InventoryItemFact>
            {
                Fact(0, null, 21, "PK-1", "MAT-1", "01", "BATCH-1", 2026, 34, 40m, false),
                Fact(0, null, 21, "PK-1", "MAT-1", "01", "BATCH-1", 2026, 34, 60m, false)
            };
            repository.ParentBoxes[21] = "BOX-1";
            FakeHuIdentifierService identifiers = new FakeHuIdentifierService();
            identifiers.Results["PK-1"] = Identity(21, "PK-1", "INNER_PACK");
            FakeEffectiveLocationResolver resolver = new FakeEffectiveLocationResolver();
            resolver.Locations[21] = new HuLocationDto { HuId = 21, EffectiveWarehouseCode = "FG01", EffectiveZoneCode = "STORAGE", EffectiveLocationCode = "H201", LocationSource = "PARENT_BOX" };
            InventoryQueryService service = new InventoryQueryService(repository, identifiers, resolver);

            InventoryInnerPackLookupDto result = (InventoryInnerPackLookupDto)service.Lookup("FG01", "PK-1").Data;
            Equal(100m, result.TotalQty, "inner pack consistent items aggregate quantity");
            Equal("2634", result.WeekCode, "inner pack week format");
            Equal("BOX-1", result.ParentBoxCode, "inner pack active parent box");
            Equal("PARENT_BOX", result.LocationSource, "inner pack source location retained");

            repository.InnerFacts[21].Add(Fact(0, null, 21, "PK-1", "MAT-1", "01", "BATCH-1", 2026, 35, 1m, false));
            OperationResult<InventoryLookupDto> conflict = service.Lookup("FG01", "PK-1");
            Equal(false, conflict.IsSuccess, "inner pack conflicting dimensions fail");
            Equal(true, conflict.Message.StartsWith("INNER_PACK_ITEM_CONFLICT:", StringComparison.Ordinal), "inner pack conflict has stable code prefix");
            Pass();
        }

        private static void InventoryLookupEnforcesResolutionAndWarehouseRules()
        {
            FakeInventoryQueryRepository repository = new FakeInventoryQueryRepository();
            repository.Hus[31] = new HuDto { Id = 31, HuCode = "BOX-31", HuType = "OUTER_BOX", LifecycleStatus = "SHIPPED", IsHold = false };
            repository.Hus[32] = new HuDto { Id = 32, HuCode = "PALLET-32", HuType = "PALLET", LifecycleStatus = "ACTIVE" };
            repository.ForeignLocations["OTHER"] = new InventoryLookupLocationRecord { Id = 99, WarehouseCode = "FG02", LocationCode = "OTHER" };
            FakeHuIdentifierService identifiers = new FakeHuIdentifierService();
            identifiers.Results["BOX-31"] = Identity(31, "BOX-31", "OUTER_BOX");
            identifiers.Results["PALLET-32"] = Identity(32, "PALLET-32", "PALLET");
            FakeEffectiveLocationResolver resolver = new FakeEffectiveLocationResolver();
            resolver.Locations[31] = new HuLocationDto { HuId = 31, EffectiveWarehouseCode = "FG01", EffectiveZoneCode = "SYSTEM", EffectiveLocationCode = "SHIPPED", LocationSource = "SELF" };
            InventoryQueryService service = new InventoryQueryService(repository, identifiers, resolver);

            InventoryOuterBoxLookupDto shipped = (InventoryOuterBoxLookupDto)service.Lookup("FG01", "BOX-31").Data;
            Equal("SHIPPED", shipped.HuStatus, "non-active HU facts remain queryable");
            Equal(string.Empty, shipped.LocationCode, "shipped is not returned as physical location");
            Equal(false, service.Lookup("FG01", "PALLET-32").IsSuccess, "unsupported pallet fails");
            Equal("货架[OTHER]不属于仓库[FG01]。", service.Lookup("FG01", "OTHER").Message, "foreign location reports warehouse mismatch");

            resolver.Locations[31] = new HuLocationDto { HuId = 31, EffectiveWarehouseCode = "FG02", EffectiveLocationCode = "H999" };
            Equal(false, service.Lookup("FG01", "BOX-31").IsSuccess, "HU effective location enforces warehouse");
            Equal(false, service.Lookup("FG01", "missing").IsSuccess, "missing lookup fails");
            Pass();
        }

        private static void WholePalletPutawayContextAndCommandAreAligned()
        {
            FakeHuRepository hus = HuHierarchy();
            hus.Hus["BOX-1"].PlantCode = "1020";
            FakePutawayRepository putaway = new FakePutawayRepository
            {
                Status = new PalletPutawayStatusDto { PalletHuId = 1, PalletHuCode = "PALLET-1", RemainingBoxCodes = new List<string> { "BOX-1" }, RemainingBoxCount = 1 }
            };
            FakeInventoryQueryRepository inventory = new FakeInventoryQueryRepository();
            inventory.LocationFacts.Add(Fact(2, "BOX-1", 3, "PACK-1", "MAT-1", "01", "B1", 2026, 34, 100m, false));
            FakeEffectiveLocationResolver locations = new FakeEffectiveLocationResolver();
            locations.Locations[1] = new HuLocationDto { HuId = 1, EffectiveWarehouseCode = "FG01", EffectiveLocationCode = "INBOUND-PENDING", LocationSource = "SELF" };
            locations.Locations[2] = new HuLocationDto { HuId = 2, EffectiveWarehouseCode = "FG01", EffectiveLocationCode = "INBOUND-PENDING", LocationSource = "PARENT_PALLET" };
            PutawayService service = new PutawayService(hus, new FakeLocationMaintenanceRepository(), new FakeHuLocationRepository(), putaway, inventory, locations);

            OperationResult<PalletPutawayByBoxContextDto> context = service.GetPalletContextByBox(" box-1 ");
            Equal(true, context.IsSuccess, "whole pallet context succeeds");
            Equal("PALLET-1", context.Data.PalletHuCode, "box resolves active pallet");
            Equal("PARENT_PALLET", context.Data.Boxes[0].LocationSource, "whole pallet box uses effective location source");
            Equal(100m, context.Data.TotalQty, "whole pallet total quantity");

            OperationResult<WholePalletPutawayResultDto> command = service.PutawayPallet(new WholePalletPutawayRequest
            {
                PalletHuCode = " pallet-1 ", WarehouseCode = " fg01 ", TargetLocationCode = " h201 ", RequestId = "REQ-PALLET", Operator = "tester"
            });
            Equal(true, command.IsSuccess, "whole pallet command succeeds");
            Equal("PALLET-1", putaway.LastWholeCommand.PalletHuCode, "whole pallet code normalized");
            Equal("H201", putaway.LastWholeCommand.TargetLocationCode, "whole pallet target normalized");
            Equal(false, service.PutawayPallet(new WholePalletPutawayRequest()).IsSuccess, "whole pallet required fields validated");
            Pass();
        }

        private static void PutawayBoxResolvesPalletInheritedMode()
        {
            FakeHuRepository hus = HuHierarchy();
            hus.Hus["PALLET-1"].PlantCode = "1010";
            hus.Hus["BOX-1"].PlantCode = "1010";
            FakePutawayRepository repository = new FakePutawayRepository
            {
                Status = new PalletPutawayStatusDto { PalletHuId = 1, PalletHuCode = "PALLET-1", RemainingBoxCodes = new List<string> { "BOX-1" }, RemainingBoxCount = 1 }
            };
            FakeHuLocationRepository huLocations = new FakeHuLocationRepository();
            huLocations.Locations.Add(new HuLocationDto { HuId = 1, DirectLocationId = 100, DirectLocationCode = "INBOUND-PENDING", DirectZoneCode = "RECEIVE", DirectWarehouseCode = "FG01", LocationSource = "SELF" });
            huLocations.Locations.Add(new HuLocationDto { HuId = 2, EffectiveLocationId = 100, EffectiveLocationCode = "INBOUND-PENDING", LocationSourceHuId = 1, LocationSource = "PARENT_PALLET" });
            FakeLocationMaintenanceRepository locations = PutawayLocations();
            PutawayService service = new PutawayService(hus, locations, huLocations, repository);

            OperationResult<PutawayResultDto> result = service.PutawayBox(new PutawayBoxRequest
            {
                BoxHuCode = "BOX-1", WarehouseCode = "FG01", ToLocationCode = "H208", RequestId = "REQ-PALLET-BOX", Operator = "tester"
            });
            Equal(true, result.IsSuccess, "pallet inherited box putaway succeeds");
            Equal("PALLET_INHERITED", result.Data.SourceMode, "pallet source mode returned");
            Equal(1, repository.PalletBoxCalls, "pallet transaction path retained");
            Equal(0, repository.DirectCalls, "pallet path does not dispatch direct transaction");
            locations.Locations["H208"].MasterStatus = "INACTIVE";
            Equal(false, service.PutawayBox(new PutawayBoxRequest { BoxHuCode = "BOX-1", WarehouseCode = "FG01", ToLocationCode = "H208", RequestId = "REQ-BAD-TARGET", Operator = "tester" }).IsSuccess, "invalid pallet target rejected before mutation");
            Equal(1, repository.PalletBoxCalls, "invalid pallet target performs no write");
            Pass();
        }

        private static void PutawayBoxResolvesDirectReceiveMode()
        {
            DirectPutawayFixture fixture = DirectPutaway();
            OperationResult<PutawayResultDto> result = fixture.Service.PutawayBox(new PutawayBoxRequest
            {
                BoxHuCode = "MERGED-1", WarehouseCode = "FG01", ToLocationCode = "H208", ScannedLocationCode = "H208",
                RequestId = "REQ-DIRECT", BusinessNo = "PDA-PUTAWAY-1", Operator = "tester"
            });
            Equal(true, result.IsSuccess, "direct receive box putaway succeeds");
            Equal("DIRECT_RECEIVE", result.Data.SourceMode, "direct receive source mode returned");
            Equal(null, result.Data.PalletHuCode, "direct receive response has no pallet");
            Equal("INBOUND-PENDING", fixture.Repository.LastDirectCommand.FromLocationCode, "direct source location preserved");
            Equal("H208", fixture.Repository.LastDirectCommand.ToLocationCode, "direct target dispatched");
            Equal(1, fixture.Repository.DirectCalls, "direct transaction dispatched once");
            Equal(0, fixture.Repository.PalletBoxCalls, "direct mode does not invoke pallet transaction");
            Pass();
        }

        private static void PutawayBoxKeepsMergedChildrenBelowDirectReceiveBox()
        {
            DirectPutawayFixture fixture = DirectPutaway();
            HuDto box = fixture.Hus.Hus["MERGED-1"];
            fixture.Hus.Hus.Remove("MERGED-1");
            box.HuCode = "B-MERGED";
            fixture.Hus.Hus[box.HuCode] = box;
            for (int index = 0; index < 3; index++)
            {
                long childId = 11 + index;
                string childCode = "INNER_PACK-" + Convert.ToChar('A' + index);
                fixture.Hus.Hus[childCode] = new HuDto { Id = childId, HuCode = childCode, HuType = "INNER_PACK", PlantCode = "1010", LifecycleStatus = "ACTIVE" };
                fixture.Hus.ParentIds[childId] = box.Id;
                fixture.Resolver.Locations[childId] = new HuLocationDto
                {
                    HuId = childId,
                    HuCode = childCode,
                    DirectLocationId = null,
                    EffectiveLocationId = 100,
                    EffectiveLocationCode = "INBOUND-PENDING",
                    EffectiveWarehouseCode = "FG01",
                    EffectiveZoneCode = "RECEIVE",
                    LocationSourceHuId = box.Id,
                    LocationSourceHuCode = box.HuCode,
                    LocationSourceHuType = "OUTER_BOX",
                    LocationSource = "PARENT_BOX"
                };
            }

            fixture.Repository.OnDirectExecute = command =>
            {
                HuLocationDto boxLocation = fixture.Resolver.Locations[box.Id];
                boxLocation.DirectLocationId = command.ToLocationId;
                boxLocation.DirectLocationCode = command.ToLocationCode;
                boxLocation.DirectZoneCode = "STORAGE";
                boxLocation.EffectiveLocationId = command.ToLocationId;
                boxLocation.EffectiveLocationCode = command.ToLocationCode;
                boxLocation.EffectiveZoneCode = "STORAGE";
                foreach (long childId in fixture.Hus.ParentIds.Where(x => x.Value == box.Id).Select(x => x.Key))
                {
                    HuLocationDto childLocation = fixture.Resolver.Locations[childId];
                    childLocation.EffectiveLocationId = command.ToLocationId;
                    childLocation.EffectiveLocationCode = command.ToLocationCode;
                    childLocation.EffectiveZoneCode = "STORAGE";
                }
            };

            Equal(null, fixture.Hus.GetActiveParent(box.Id), "merged INNER_PACK children do not make OUTER_BOX appear to have a parent");
            Equal(null, fixture.HuLocations.Locations[0].LocationSource, "legacy direct-location projection reproduces missing source enum");
            OperationResult<PutawayResultDto> result = fixture.Service.PutawayBox(new PutawayBoxRequest
            {
                BoxHuCode = box.HuCode,
                WarehouseCode = "FG01",
                ToLocationCode = "H208",
                ScannedLocationCode = "H208",
                RequestId = "REQ-MERGED-DIRECT",
                Operator = "tester"
            });

            Equal(true, result.IsSuccess, "merged box direct receive putaway succeeds through effective resolver");
            Equal("DIRECT_RECEIVE", result.Data.SourceMode, "merged box resolves direct receive source mode");
            HuLocationDto movedBox = fixture.Resolver.Resolve(box.Id);
            Equal("H208", movedBox.DirectLocationCode, "merged box direct location moves to storage");
            Equal("SELF", movedBox.LocationSource, "merged box remains self located");
            foreach (long childId in fixture.Hus.ParentIds.Where(x => x.Value == box.Id).Select(x => x.Key))
            {
                HuLocationDto child = fixture.Resolver.Resolve(childId);
                Equal(null, child.DirectLocationId, "merged child direct location remains null");
                Equal(box.Id, child.LocationSourceHuId, "merged child parent box remains unchanged");
                Equal("H208", child.EffectiveLocationCode, "merged child inherits moved box location");
                Equal("PARENT_BOX", child.LocationSource, "merged child location source remains parent box");
            }
            Pass();
        }

        private static void PutawayBoxRejectsInvalidDirectStates()
        {
            DirectPutawayFixture fixture = DirectPutaway();
            fixture.Resolver.Locations[10].DirectZoneCode = "STORAGE";
            Equal(true, fixture.Service.PutawayBox(PutawayRequest("REQ-STORAGE")).Message.Contains("移库"), "self storage directs operator to relocation");

            fixture = DirectPutaway();
            fixture.Resolver.Locations.Clear();
            Equal(true, fixture.Service.PutawayBox(PutawayRequest("REQ-NO-LOCATION")).Message.Contains("位置状态不完整"), "missing direct location rejected");

            fixture = DirectPutaway();
            fixture.Resolver.Locations[10].EffectiveLocationId = 101;
            fixture.Resolver.Locations[10].LocationSourceHuId = 11;
            fixture.Resolver.Locations[10].LocationSource = "PARENT_BOX";
            Equal("外箱直接位置与有效位置来源不一致，无法执行上架。", fixture.Service.PutawayBox(PutawayRequest("REQ-LOCATION-CONFLICT")).Message, "true direct and effective location conflict remains rejected");

            fixture = DirectPutaway();
            fixture.Hus.Hus["MERGED-1"].IsHold = true;
            Equal(true, fixture.Service.PutawayBox(PutawayRequest("REQ-HOLD")).Message.Contains("冻结"), "direct hold rejected");

            fixture = DirectPutaway();
            fixture.Hus.Hus["MERGED-1"].LifecycleStatus = "SHIPPED";
            Equal(true, fixture.Service.PutawayBox(PutawayRequest("REQ-SHIPPED")).Message.Contains("ACTIVE"), "direct non-active rejected");

            fixture = DirectPutaway();
            fixture.Hus.Hus["PARENT-BOX"] = new HuDto { Id = 11, HuCode = "PARENT-BOX", HuType = "OUTER_BOX", PlantCode = "1010", LifecycleStatus = "ACTIVE" };
            fixture.Hus.ParentIds[10] = 11;
            Equal(true, fixture.Service.PutawayBox(PutawayRequest("REQ-BAD-PARENT")).Message.Contains("不是PALLET"), "non-pallet parent rejected");
            Equal(0, fixture.Repository.DirectCalls, "invalid direct states perform no mutation");
            Pass();
        }

        private static void PutawayBoxDirectReplayIsSafe()
        {
            DirectPutawayFixture fixture = DirectPutaway();
            fixture.Repository.Existing = new PutawayResultDto
            {
                SourceMode = "DIRECT_RECEIVE", BoxHuCode = "MERGED-1", FromLocationCode = "INBOUND-PENDING",
                ToLocationCode = "H208", IsDuplicate = false
            };
            OperationResult<PutawayResultDto> replay = fixture.Service.PutawayBox(PutawayRequest("REQ-REPLAY"));
            Equal(true, replay.IsSuccess, "direct retry succeeds without pallet");
            Equal(true, replay.Data.IsDuplicate, "direct retry marked duplicate");
            Equal("DIRECT_RECEIVE", replay.Data.SourceMode, "direct replay preserves source mode");
            Equal(0, fixture.Repository.DirectCalls, "direct replay performs no second move");
            PutawayBoxRequest conflict = PutawayRequest("REQ-REPLAY");
            conflict.ToLocationCode = "H305";
            Equal(false, fixture.Service.PutawayBox(conflict).IsSuccess, "same request id with different target rejected");
            Pass();
        }

        private static void DirectReceivePutawayRepositoryIsAtomic()
        {
            string repository = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "DirectReceivePutawayRepository.cs"));
            Equal(true, repository.Contains("IsolationLevel.Serializable") && repository.Contains("WITH (UPDLOCK,HOLDLOCK)"), "direct receive uses serializable locks");
            Equal(true, repository.Contains("HuMoveLogWriter.Insert") && repository.Contains("\"PUTAWAY\"") && repository.Contains("UPDATE dbo.WMS_HU_CurrentLocation"), "direct receive atomically moves box and audits putaway");
            Equal(true, repository.Contains("RequestIdExists") && repository.Contains("PutawayIdempotencyReader.Read"), "direct transaction rechecks idempotency before mutation");
            Equal(false, repository.Contains("INSERT INTO dbo.WMS_HU_Relation") || repository.Contains("UPDATE dbo.WMS_HU_Relation") || repository.Contains("\"DETACH_HU\""), "direct receive creates no fake pallet and no detach audit");
            Equal(false, repository.Contains("INNER_PACK") || repository.Contains("AddWithValue") || repository.Contains("dynamic"), "direct receive does not mutate children and uses typed code");
            Pass();
        }

        private static void PalletPutawayRepositoryContractRemains()
        {
            string repository = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "PutawayRepository.cs"));
            Equal(true, repository.Contains("CloseReason = N'PUTAWAY'") && repository.Contains("\"DETACH_HU\""), "pallet relation detach remains");
            Equal(true, repository.Contains("RemainingBoxCount") && repository.Contains("CLOSE_HU"), "pallet remaining and close semantics remain");
            Equal(true, repository.Contains("SourceMode = \"PALLET_INHERITED\""), "pallet response identifies derived source mode");
            Equal(true, repository.Contains("ExecuteDirectReceivePutaway") && repository.Contains("new DirectReceivePutawayRepository().Execute"), "putaway repository cleanly separates direct transaction");
            Pass();
        }

        private static void PutawayReplayAndChildInheritanceContractsAlign()
        {
            string reader = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "PutawayIdempotencyReader.cs"));
            string resolver = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "EffectiveLocationResolver.cs"));
            Equal(true, reader.Contains("LEFT JOIN dbo.WMS_HU pallet") && reader.Contains("DIRECT_RECEIVE") && reader.Contains("PALLET_INHERITED"), "idempotency reader supports nullable pallet relation");
            Equal(false, reader.Contains("INNER JOIN dbo.WMS_HU pallet"), "direct replay does not require pallet join");
            Equal(true, resolver.Contains("PARENT_BOX") && resolver.Contains("CurrentLocation"), "children continue inheriting moved parent box location");
            Pass();
        }

        private static void PutawayRecommendationValidatesSourcesAndConflicts()
        {
            PutawayRecommendationSnapshot direct = RecommendationSnapshot("SELF");
            direct.Locations.Add(RecommendationLocation(210, "H210", "H12", "H2", 10));
            OperationResult<PutawayRecommendationResultDto> directResult = Recommend(direct);
            Equal(true, directResult.IsSuccess, "direct receive recommendation succeeds");
            Equal("V1_DYNAMIC_CLUSTER", directResult.Data.RecommendationVersion, "recommendation version returned");

            PutawayRecommendationSnapshot inherited = RecommendationSnapshot("PARENT_PALLET");
            inherited.Locations.Add(RecommendationLocation(211, "H211", "H12", "H2", 11));
            Equal(true, Recommend(inherited).IsSuccess, "pallet inherited recommendation succeeds");

            inherited.Box.EffectiveZoneCode = "STORAGE";
            Equal(false, Recommend(inherited).IsSuccess, "already stored box is not a putaway recommendation input");
            inherited.Box.EffectiveZoneCode = "RECEIVE";
            inherited.IncomingItems.Add(new PutawayRecommendationItemSnapshot { MaterialCode = "MAT-B", MaterialRevision = "82" });
            Equal(true, Recommend(inherited).Message.StartsWith("PUTAWAY_RECOMMENDATION_ITEM_CONFLICT:"), "material conflict is explicit");
            Pass();
        }

        private static void PutawayRecommendationKeepsSingleLocationCluster()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(209, "H209", "H12", "H2", 9));
            snapshot.Locations.Add(RecommendationLocation(210, "H210", "H12", "H2", 10));
            snapshot.Locations.Add(RecommendationLocation(211, "H211", "H12", "H2", 11));
            AddRecommendationBoxes(snapshot, 210, 3, "MAT-A", "82", 2026, 34, 1000);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal("H210", result.RecommendedLocationCode, "single cluster remains at occupied location");
            Equal("EXISTING_CLUSTER", result.Recommendations[0].RecommendationLevel, "single cluster level");
            Equal("H211", result.Recommendations[1].LocationCode, "single cluster forward expansion is second");
            Equal("H209", result.Recommendations[2].LocationCode, "single cluster backward expansion is third");
            Pass();
        }

        private static void PutawayRecommendationKeepsExistingClusterInMixedLegacyLocation()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.IncomingItems[0].MaterialCode = "16I080019D";
            snapshot.IncomingItems[0].MaterialRevision = "82";
            snapshot.IncomingItems[0].WeekYear = 2026;
            snapshot.IncomingItems[0].WeekNo = 32;
            snapshot.Locations.Add(RecommendationLocation(25, "H207", "H12", "H2", 7));
            snapshot.Locations.Add(RecommendationLocation(26, "H208", "H12", "H2", 8));
            snapshot.Locations.Add(RecommendationLocation(27, "H209", "H12", "H2", 9));
            AddRecommendationBoxes(snapshot, 26, 4, "16I080019D", "82", 2026, 32, 1951241);
            AddRecommendationBoxes(snapshot, 26, 1, "OTHER-MATERIAL", "01", 2026, 32, 1951122);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal("H208", result.RecommendedLocationCode, "existing target cluster survives legacy mixed inventory at no-mix location");
            Equal("EXISTING_CLUSTER", result.Recommendations[0].RecommendationLevel, "legacy mixed location keeps existing-cluster level");
            Equal("H209", result.Recommendations[1].LocationCode, "forward expansion follows existing H208 cluster");
            Equal("H207", result.Recommendations[2].LocationCode, "backward expansion remains third");
            Pass();
        }

        private static void PutawayRecommendationDynamicallyFillsAndExpands()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(210, "H210", "H12", "H2", 10));
            snapshot.Locations.Add(RecommendationLocation(211, "H211", "H12", "H2", 11));
            snapshot.Locations.Add(RecommendationLocation(212, "H212", "H12", "H2", 12));
            AddRecommendationBoxes(snapshot, 210, 10, "MAT-A", "82", 2026, 33, 2000);
            AddRecommendationBoxes(snapshot, 211, 9, "MAT-A", "82", 2026, 34, 3000);

            PutawayRecommendationResultDto unbalanced = Recommend(snapshot).Data;
            Equal("H211", unbalanced.RecommendedLocationCode, "lower cluster count fills first");
            Equal("CLUSTER_FILL", unbalanced.Recommendations[0].RecommendationLevel, "unbalanced level");
            Equal(1, unbalanced.Recommendations[0].ClusterFillGap, "dynamic fill gap");

            AddRecommendationBoxes(snapshot, 211, 1, "MAT-A", "82", 2026, 34, 4000);
            PutawayRecommendationResultDto balanced = Recommend(snapshot).Data;
            Equal("H212", balanced.RecommendedLocationCode, "balanced cluster expands");
            Equal("CLUSTER_EXPANSION", balanced.Recommendations[0].RecommendationLevel, "balanced expansion level");
            Equal(0, balanced.Recommendations[0].ClusterFillGap, "balanced dynamic gap");
            Pass();
        }

        private static void PutawayRecommendationRanksMultiLocationAndTop3()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            for (int position = 8; position <= 12; position++)
                snapshot.Locations.Add(RecommendationLocation(position, "H2" + position.ToString("00"), "H12", "H2", position));
            AddRecommendationBoxes(snapshot, 8, 10, "MAT-A", "82", 2026, 34, 5000);
            AddRecommendationBoxes(snapshot, 9, 10, "MAT-A", "82", 2026, 34, 5100);
            AddRecommendationBoxes(snapshot, 10, 8, "MAT-A", "82", 2026, 34, 5200);
            AddRecommendationBoxes(snapshot, 11, 9, "MAT-A", "82", 2026, 34, 5300);
            AddRecommendationBoxes(snapshot, 12, 9, "MAT-A", "82", 2026, 34, 5400);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal(3, result.Recommendations.Count, "recommendation takes global top three");
            Equal("H210", result.Recommendations[0].LocationCode, "count eight ranks first");
            Equal("H211", result.Recommendations[1].LocationCode, "count nine ranks second");
            Equal("CLUSTER_FILL", result.Recommendations[0].RecommendationLevel, "first tier is fill");
            Equal(true, result.Recommendations.All(x => x.RecommendationLevel == "CLUSTER_FILL"), "all top three may share one level");
            Equal(1, result.Recommendations[0].Rank, "first rank assigned");
            Equal(3, result.Recommendations[2].Rank, "third rank assigned");
            Equal(result.Recommendations[0].LocationCode, result.RecommendedLocationCode, "top one mirrors recommended location");
            Pass();
        }

        private static void PutawayRecommendationPrefersForwardExpansion()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(209, "H209", "H12", "H2", 9));
            snapshot.Locations.Add(RecommendationLocation(210, "H210", "H12", "H2", 10));
            snapshot.Locations.Add(RecommendationLocation(211, "H211", "H12", "H2", 11));
            snapshot.Locations.Add(RecommendationLocation(212, "H212", "H12", "H2", 12));
            AddRecommendationBoxes(snapshot, 210, 4, "MAT-A", "82", 2026, 34, 6000);
            AddRecommendationBoxes(snapshot, 211, 4, "MAT-A", "82", 2026, 34, 6100);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal("H212", result.Recommendations[0].LocationCode, "forward boundary ranks before backward");
            Equal("H209", result.Recommendations[1].LocationCode, "backward boundary remains candidate");
            Equal("CLUSTER_EXPANSION", result.Recommendations[0].RecommendationLevel, "forward expansion level");
            Pass();
        }

        private static void PutawayRecommendationUsesPairedRowsWithoutInventingLocations()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(210, "H210", "H12", "H2", 10));
            snapshot.Locations.Add(RecommendationLocation(211, "H211", "H12", "H2", 11));
            snapshot.Locations.Add(RecommendationLocation(212, "H212", "H12", "H2", 12));
            snapshot.Locations.Add(RecommendationLocation(110, "H110", "H12", "H1", 10));
            snapshot.Locations.Add(RecommendationLocation(111, "H111", "H12", "H1", 11));
            snapshot.Locations.Add(RecommendationLocation(301, "H301", "H34", "H3", 1));
            AddRecommendationBoxes(snapshot, 210, 2, "MAT-A", "82", 2026, 34, 7000);
            AddRecommendationBoxes(snapshot, 211, 2, "MAT-A", "82", 2026, 34, 7100);
            AddRecommendationBoxes(snapshot, 212, 2, "MAT-A", "82", 2026, 34, 7200);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal("H111", result.Recommendations[0].LocationCode, "nearest paired row position ranks first");
            Equal("PAIRED_ROW_NEARBY", result.Recommendations[0].RecommendationLevel, "paired row level");
            Equal(false, result.Recommendations.Any(x => x.LocationCode == "H813"), "algorithm never invents missing H813");
            Equal(true, result.Recommendations.FindIndex(x => x.LocationCode == "H301") < 0 || result.Recommendations.FindIndex(x => x.LocationCode == "H111") < result.Recommendations.FindIndex(x => x.LocationCode == "H301"), "paired rack precedes unrelated rack");

            PutawayRecommendationSnapshot h913 = RecommendationSnapshot("SELF");
            h913.Locations.Add(RecommendationLocation(913, "H913", "H89", "H9", 13));
            h913.Locations.Add(RecommendationLocation(812, "H812", "H89", "H8", 12));
            AddRecommendationBoxes(h913, 913, 1, "MAT-A", "82", 2026, 34, 7300);
            PutawayRecommendationResultDto edge = Recommend(h913).Data;
            Equal("H913", edge.RecommendedLocationCode, "H913 remains a valid single-location cluster");
            Equal(false, edge.Recommendations.Any(x => x.LocationCode == "H813"), "H913 pairing does not create H813");
            Pass();
        }

        private static void PutawayRecommendationUsesWeekOnlyAsTieBreaker()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(209, "H209", "H12", "H2", 9));
            snapshot.Locations.Add(RecommendationLocation(210, "H210", "H12", "H2", 10));
            snapshot.Locations.Add(RecommendationLocation(211, "H211", "H12", "H2", 11));
            PutawayRecommendationLocationSnapshot mixed = RecommendationLocation(305, "H305", "H34", "H3", 5);
            mixed.AllowMixedMaterial = true;
            snapshot.Locations.Add(mixed);
            AddRecommendationBoxes(snapshot, 209, 1, "MAT-A", "82", 2025, 1, 8000);
            AddRecommendationBoxes(snapshot, 210, 1, "MAT-A", "82", 2026, 34, 8100);
            AddRecommendationBoxes(snapshot, 211, 2, "MAT-A", "82", 2026, 20, 8200);
            AddRecommendationBoxes(snapshot, 305, 1, "MAT-A", "82", 2026, 34, 8300);
            AddRecommendationBoxes(snapshot, 305, 1, "MAT-X", "01", 2026, 34, 8400);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal("H210", result.Recommendations[0].LocationCode, "exact week wins equal fill candidates");
            Equal("SAME", result.Recommendations[0].WeekAffinity, "exact week affinity exposed");
            Equal("CLUSTER_FILL", result.Recommendations[0].RecommendationLevel, "week does not change recommendation tier");
            Equal(true, result.Recommendations.All(x => x.RecommendationLevel != "MIXED_FALLBACK") || result.Recommendations.FindIndex(x => x.RecommendationLevel == "CLUSTER_FILL") < result.Recommendations.FindIndex(x => x.RecommendationLevel == "MIXED_FALLBACK"), "lower mixed tier cannot jump by week");
            Pass();
        }

        private static void PutawayRecommendationDoesNotGuessMissingTopology()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(210, "H210", null, null, null));
            PutawayRecommendationLocationSnapshot numericNeighbor = RecommendationLocation(211, "H211", null, null, null);
            numericNeighbor.PutawayPriority = 1;
            snapshot.Locations.Add(numericNeighbor);
            snapshot.Locations.Add(RecommendationLocation(212, "H212", "H12", "H2", 12));
            snapshot.Locations.Add(RecommendationLocation(301, "H301", "H34", "H3", 1));
            AddRecommendationBoxes(snapshot, 210, 3, "MAT-A", "82", 2026, 34, 9000);

            PutawayRecommendationResultDto result = Recommend(snapshot).Data;
            Equal("H210", result.RecommendedLocationCode, "missing topology still keeps existing material location");
            Equal("EXISTING_CLUSTER", result.Recommendations[0].RecommendationLevel, "missing topology remains single cluster");
            Equal("EMPTY", result.Recommendations.First(x => x.LocationCode == "H211").RecommendationLevel, "numeric neighbor is not guessed as expansion");
            Equal(false, result.Recommendations.Any(x => x.RecommendationLevel == "PAIRED_ROW_NEARBY"), "missing topology has no paired-row semantics");

            PutawayRecommendationSnapshot legality = RecommendationSnapshot("SELF");
            PutawayRecommendationLocationSnapshot dedicated = RecommendationLocation(401, "H401", "H34", "H4", 1);
            dedicated.PlantPolicy = "DEDICATED";
            dedicated.DedicatedPlantCode = "1020";
            legality.Locations.Add(dedicated);
            PutawayRecommendationLocationSnapshot noMix = RecommendationLocation(402, "H402", "H34", "H4", 2);
            legality.Locations.Add(noMix);
            legality.Locations.Add(RecommendationLocation(403, "H403", "H34", "H4", 3));
            AddRecommendationBoxes(legality, 402, 1, "MAT-X", "01", 2026, 34, 9100);
            PutawayRecommendationResultDto legalResult = Recommend(legality).Data;
            Equal(1, legalResult.Recommendations.Count, "illegal candidates are not added to reach top three");
            Equal("H403", legalResult.RecommendedLocationCode, "only legal storage candidate remains");
            Pass();
        }

        private static void PutawayRecommendationRepositoryIsReadOnlyAndCapacityFree()
        {
            string repository = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "PutawayRecommendationRepository.cs"));
            string service = File.ReadAllText(Path.Combine("WMS-HU-Model", "Services", "Impl", "PutawayRecommendationService.cs"));
            string migration = File.ReadAllText(Path.Combine("docs", "database", "017_LocationPhysicalTopology.sql"));
            string module = File.ReadAllText(Path.Combine("WMS", "Modules", "PutawayModule.cs"));
            Equal(false, repository.Contains("INSERT INTO") || repository.Contains("UPDATE dbo") || repository.Contains("DELETE FROM") || repository.Contains("MERGE "), "recommendation repository is read-only");
            Equal(false, repository.Contains("AddWithValue") || repository.Contains("dynamic"), "recommendation repository uses typed ADO.NET");
            Equal(true, repository.Contains("EffectiveLocationResolver.ActiveParentPredicateSql") && repository.Contains("relation.IsActive = 1"), "recommendation inventory uses effective location ancestry");
            Equal(true, repository.Contains("zone.ZoneCode = 'STORAGE'") && repository.Contains("location.MasterStatus = 'ACTIVE'") && repository.Contains("location.IsVirtual = 0") && repository.Contains("location.IsScannable = 1"), "recommendation repository applies hard storage filters");
            Equal(false, repository.Contains("box.IsHold = 0"), "held boxes remain in physical occupancy");
            Equal(false, service.Contains("CapacityQty") || service.Contains("MaxWeightKg") || service.Contains("PreferredFillCount"), "V1 ranking has no capacity assumption");
            Equal(true, migration.Contains("PhysicalRackGroupCode") && migration.Contains("PhysicalRowCode") && migration.Contains("PositionSequence"), "topology migration adds minimal fields");
            Equal(true, migration.Contains("'H913','H89','H9',13") && !migration.Contains("H813"), "confirmed H topology includes H913 without H813");
            Equal(false, migration.Contains("LocationAdjacency"), "no adjacency table created");
            Equal(true, module.Contains("Get(\"/recommendations\"") && module.Contains("Request.Query.warehouseCode") && module.Contains("Request.Query.boxHuCode"), "public recommendation route registered");
            Pass();
        }

        private static void PutawayRecommendationDoesNotBlockManualOverride()
        {
            PutawayRecommendationSnapshot snapshot = RecommendationSnapshot("SELF");
            snapshot.Locations.Add(RecommendationLocation(208, "H208", "H12", "H2", 8));
            PutawayRecommendationResultDto recommendation = Recommend(snapshot).Data;
            Equal("H208", recommendation.RecommendedLocationCode, "test has a top recommendation");

            DirectPutawayFixture fixture = DirectPutaway();
            PutawayBoxRequest request = PutawayRequest("REQ-MANUAL-OVERRIDE");
            request.ToLocationCode = "H305";
            request.ScannedLocationCode = "H305";
            OperationResult<PutawayResultDto> actual = fixture.Service.PutawayBox(request);
            Equal(true, actual.IsSuccess, "valid manually scanned location still succeeds");
            Equal("H305", fixture.Repository.LastDirectCommand.ToLocationCode, "actual scan remains authoritative");
            Pass();
        }

        private static DirectPutawayFixture DirectPutaway()
        {
            DirectPutawayFixture fixture = new DirectPutawayFixture();
            fixture.Hus.Hus["MERGED-1"] = new HuDto { Id = 10, HuCode = "MERGED-1", HuType = "OUTER_BOX", PlantCode = "1010", LifecycleStatus = "ACTIVE", IsHold = false };
            fixture.HuLocations.Locations.Add(new HuLocationDto
            {
                HuId = 10, DirectLocationId = 100, DirectLocationCode = "INBOUND-PENDING", DirectWarehouseCode = "FG01",
                DirectZoneCode = "RECEIVE", EffectiveLocationId = 100, EffectiveLocationCode = "INBOUND-PENDING", LocationSourceHuId = 10
            });
            fixture.Resolver.Locations[10] = new HuLocationDto
            {
                HuId = 10, HuCode = "MERGED-1", HuType = "OUTER_BOX", DirectLocationId = 100, DirectLocationCode = "INBOUND-PENDING",
                DirectWarehouseCode = "FG01", DirectZoneCode = "RECEIVE", EffectiveLocationId = 100, EffectiveLocationCode = "INBOUND-PENDING",
                EffectiveWarehouseCode = "FG01", EffectiveZoneCode = "RECEIVE", LocationSourceHuId = 10, LocationSourceHuCode = "MERGED-1",
                LocationSourceHuType = "OUTER_BOX", LocationSource = "SELF"
            };
            fixture.Locations = PutawayLocations();
            fixture.Service = new PutawayService(fixture.Hus, fixture.Locations, fixture.HuLocations, fixture.Repository, new FakeInventoryQueryRepository(), fixture.Resolver);
            return fixture;
        }

        private static FakeLocationMaintenanceRepository PutawayLocations()
        {
            FakeLocationMaintenanceRepository result = new FakeLocationMaintenanceRepository();
            result.Locations["INBOUND-PENDING"] = new LocationDto { Id = 100, WarehouseCode = "FG01", LocationCode = "INBOUND-PENDING", ZoneCode = "RECEIVE", MasterStatus = "ACTIVE", IsVirtual = true, PlantPolicy = "ANY" };
            result.Locations["H208"] = new LocationDto { Id = 200, WarehouseCode = "FG01", LocationCode = "H208", ZoneCode = "STORAGE", MasterStatus = "ACTIVE", IsVirtual = false, PlantPolicy = "ANY" };
            result.Locations["H305"] = new LocationDto { Id = 201, WarehouseCode = "FG01", LocationCode = "H305", ZoneCode = "STORAGE", MasterStatus = "ACTIVE", IsVirtual = false, PlantPolicy = "ANY" };
            return result;
        }

        private static PutawayBoxRequest PutawayRequest(string requestId)
        {
            return new PutawayBoxRequest { BoxHuCode = "MERGED-1", WarehouseCode = "FG01", ToLocationCode = "H208", RequestId = requestId, Operator = "tester" };
        }

        private static void BatchRelocationValidatesAtomicRequest()
        {
            FakeRelocationRepository repository = new FakeRelocationRepository();
            RelocationService service = new RelocationService(repository);
            OperationResult<BatchRelocationResultDto> result = service.MoveBatch(new BatchRelocationRequest
            {
                WarehouseCode = " fg01 ", SourceLocationCode = " h201 ", TargetLocationCode = " h305 ",
                BoxHuCodes = new List<string> { " box-2 ", "box-1" }, RequestId = "REQ-MOVE", Operator = "tester"
            });
            Equal(true, result.IsSuccess, "batch relocation succeeds");
            Equal("FG01", repository.LastCommand.WarehouseCode, "batch relocation warehouse normalized");
            Equal(2, repository.LastCommand.BoxHuCodes.Count, "batch relocation dispatches one atomic command");
            Equal(false, service.MoveBatch(new BatchRelocationRequest { WarehouseCode = "FG01", SourceLocationCode = "H201", TargetLocationCode = "H305", BoxHuCodes = new List<string> { "B1", "b1" }, RequestId = "R", Operator = "O" }).IsSuccess, "batch relocation rejects duplicates");
            Equal(false, service.MoveBatch(new BatchRelocationRequest { WarehouseCode = "FG01", SourceLocationCode = "H201", TargetLocationCode = "H201", BoxHuCodes = new List<string> { "B1" }, RequestId = "R", Operator = "O" }).IsSuccess, "batch relocation rejects same location");
            Pass();
        }

        private static void BatchRelocationHandlesReplayAndWarehouseValidationFailures()
        {
            FakeRelocationRepository duplicateRepository = new FakeRelocationRepository
            {
                Existing = new BatchRelocationResultDto
                {
                    SourceLocationCode = "H201",
                    TargetLocationCode = "H305",
                    BoxCodes = new List<string> { "BOX-1" },
                    MovedCount = 1
                }
            };
            RelocationService duplicateService = new RelocationService(duplicateRepository);
            OperationResult<BatchRelocationResultDto> duplicate = duplicateService.MoveBatch(new BatchRelocationRequest
            {
                WarehouseCode = "FG01", SourceLocationCode = "H201", TargetLocationCode = "H305",
                BoxHuCodes = new List<string> { "BOX-1" }, RequestId = "REQ-MOVE", Operator = "tester"
            });
            Equal(true, duplicate.IsSuccess, "batch relocation replays same request");
            Equal(true, duplicate.Data.IsDuplicate, "batch relocation marks replay");
            Equal(null, duplicateRepository.LastCommand, "batch relocation replay skips write");

            FakeRelocationRepository sourceFailureRepository = new FakeRelocationRepository
            {
                Next = BatchRelocationPersistenceResult.Fail("一个或多个外箱的有效源货位已变化。")
            };
            OperationResult<BatchRelocationResultDto> sourceFailure = new RelocationService(sourceFailureRepository).MoveBatch(new BatchRelocationRequest
            {
                WarehouseCode = "FG01", SourceLocationCode = "H201", TargetLocationCode = "H305",
                BoxHuCodes = new List<string> { "BOX-1" }, RequestId = "REQ-SOURCE", Operator = "tester"
            });
            Equal(false, sourceFailure.IsSuccess, "batch relocation rejects changed source location");

            FakeRelocationRepository targetFailureRepository = new FakeRelocationRepository
            {
                Next = BatchRelocationPersistenceResult.Fail("目标货位不存在或不属于指定仓库。")
            };
            OperationResult<BatchRelocationResultDto> targetFailure = new RelocationService(targetFailureRepository).MoveBatch(new BatchRelocationRequest
            {
                WarehouseCode = "FG01", SourceLocationCode = "H201", TargetLocationCode = "H305",
                BoxHuCodes = new List<string> { "BOX-1" }, RequestId = "REQ-TARGET", Operator = "tester"
            });
            Equal(false, targetFailure.IsSuccess, "batch relocation rejects invalid target location");
            Pass();
        }

        private static void OutboundPickingStateResumesWithoutWrites()
        {
            FakeFormalOutboundRepository repository = new FakeFormalOutboundRepository
            {
                PickingState = new OutboundPickingStateDto
                {
                    SAPDeliveryNo = "800001", OrderStatus = "PICKING", HasActivePicking = true,
                    AllocationNo = "ALLOC-1", AllocationStatus = "PICKING",
                    RemainingTasks = new List<PickingStateTaskDto> { new PickingStateTaskDto { PickBatchNo = "PB-1", BoxCode = "BOX-1", RemainingQty = 100m } }
                }
            };
            FormalOutboundService service = new FormalOutboundService(repository, new FakeHuIdentifierService());
            OperationResult<OutboundPickingStateDto> result = service.GetPickingState(" 800001 ");
            Equal(true, result.IsSuccess, "picking state succeeds");
            Equal("PICKING", result.Data.OrderStatus, "picking state resumes existing status");
            Equal(1, result.Data.RemainingTasks.Count, "picking state returns remaining task");
            Equal(0, repository.WriteCalls, "picking state performs no repository write");
            repository.PickingStateError = "PICKING_STATE_CONFLICT: duplicate";
            Equal(false, service.GetPickingState("800001").IsSuccess, "picking state rejects inconsistent active allocations");
            Pass();
        }

        private static void WholeBoxUnpackUsesAtomicCommandContract()
        {
            FakeRepackRepository repository = new FakeRepackRepository();
            RepackService service = new RepackService(new FakeHuRepository(), new FakeLocationMaintenanceRepository(), new FakeHuLocationRepository(), repository);
            OperationResult<UnpackBoxResultDto> result = service.UnpackBox(new UnpackBoxRequest
            {
                BoxHuCode = " box-1 ", WarehouseCode = " fg01 ", RequestId = "REQ-UNPACK", Operator = "tester"
            });
            Equal(true, result.IsSuccess, "whole box unpack succeeds");
            Equal("BOX-1", repository.LastUnpackBoxCommand.BoxHuCode, "whole box code normalized");
            Equal(null, repository.LastUnpackBoxCommand.ToLocationCode, "omitted target delegates same-location semantics to transaction repository");
            Equal(false, service.UnpackBox(new UnpackBoxRequest()).IsSuccess, "whole box unpack required fields validated");
            Pass();
        }

        private static void PdaClosureRepositoriesAreAtomicAndTyped()
        {
            string putaway = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "PutawayRepository.cs"));
            string relocation = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "RelocationRepository.cs"));
            string unpack = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "WholeBoxUnpackRepository.cs"));
            string outbound = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "FormalOutboundRepository.cs"));
            Equal(true, putaway.Contains("ExecuteWholePalletPutaway") && putaway.Contains("IsolationLevel.Serializable"), "whole pallet repository is transactional");
            Equal(true, relocation.Contains("ExecuteBatch") && relocation.Contains("IsolationLevel.Serializable") && relocation.Contains("IN (@") == false, "relocation repository is transactional and binds generated parameters");
            Equal(true, unpack.Contains("Execute(UnpackBoxCommand") && unpack.Contains("IsolationLevel.Serializable"), "whole box unpack repository is transactional");
            Equal(true, outbound.Contains("GetPickingState") && outbound.Contains("ReservationStatus='ACTIVE'"), "picking state reads active reservations");
            string combined = putaway + relocation + unpack;
            Equal(false, combined.Contains("AddWithValue"), "PDA closure repositories do not use AddWithValue");
            Equal(true, combined.Contains("SqlDbType.VarChar") && combined.Contains("SqlDbType.BigInt"), "PDA closure repositories use typed parameters");
            Pass();
        }

        private static void CanonicalBoxCodeFormatsPlants()
        {
            DateTime date = new DateTime(2026, 8, 26);
            Equal("B2608260001A", CanonicalOuterBoxCode.Build(date, "1010", 1), "plant 1010 canonical box code");
            Equal("B2608260001B", CanonicalOuterBoxCode.Build(date, "1020", 1), "plant 1020 canonical box code");
            Equal(12, CanonicalOuterBoxCode.Build(date, "1010", 9999).Length, "canonical box code fixed length");
            Pass();
        }

        private static void CanonicalBoxCodeScopesAndResetsSequence()
        {
            DateTime dayOne = new DateTime(2026, 8, 26);
            DateTime dayTwo = dayOne.AddDays(1);
            Equal("B2608260001A", CanonicalOuterBoxCode.Build(dayOne, "1010", 1), "1010 independent first sequence");
            Equal("B2608260001B", CanonicalOuterBoxCode.Build(dayOne, "1020", 1), "1020 independent first sequence");
            Equal("B2608260003A", CanonicalOuterBoxCode.Build(dayOne, "1010", 3), "same plant increments");
            Equal("B2608270001A", CanonicalOuterBoxCode.Build(dayTwo, "1010", 1), "new date resets sequence scope");
            Pass();
        }

        private static void CanonicalBoxCodeRejectsInvalidInputs()
        {
            bool exhaustedRejected = false;
            bool unsupportedRejected = false;
            try { CanonicalOuterBoxCode.Build(new DateTime(2026, 8, 26), "1010", 10000); }
            catch (ArgumentOutOfRangeException) { exhaustedRejected = true; }
            try { CanonicalOuterBoxCode.Build(new DateTime(2026, 8, 26), "9999", 1); }
            catch (ArgumentException) { unsupportedRejected = true; }
            Equal(true, exhaustedRejected, "five digit daily sequence rejected");
            Equal(true, unsupportedRejected, "unsupported plant rejected");
            Pass();
        }

        private static void MergeBoxRulesAllowCrossLocationInventory()
        {
            List<string> codes = new List<string> { "I001", "I002", "I003" };
            List<MergeBoxPackFact> facts = new List<MergeBoxPackFact>
            {
                MergeFact(1, "I001", "H201", "1010", "FG01", "MAT-1", "41", 20m),
                MergeFact(2, "I002", "H208", "1010", "FG01", "MAT-1", "41", 30m),
                MergeFact(3, "I003", "H305", "1010", "FG01", "MAT-1", "41", 30m)
            };
            Equal(null, MergeBoxRules.Validate(codes, facts, "FG01"), "different STORAGE racks are accepted");
            Equal(80m, facts.Sum(x => x.TotalQty), "merge quantity sums active inventory");
            Pass();
        }

        private static void MergeBoxRulesRejectInvalidInventory()
        {
            List<string> codes = new List<string> { "I001", "I002" };
            MergeBoxPackFact valid = MergeFact(1, "I001", "H201", "1010", "FG01", "MAT-1", "41", 20m);
            MergeBoxPackFact invalid = MergeFact(2, "I002", "H208", "1010", "FG01", "MAT-1", "41", 30m);
            invalid.IsHold = true;
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { valid, invalid }, "FG01").Contains("冻结"), "hold rejects whole merge");
            invalid.IsHold = false; invalid.LifecycleStatus = "SHIPPED";
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { valid, invalid }, "FG01").Contains("ACTIVE"), "non-active rejects whole merge");
            invalid.LifecycleStatus = "ACTIVE"; invalid.HasActiveParent = true;
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { valid, invalid }, "FG01").Contains("父HU"), "active parent rejects whole merge");
            invalid.HasActiveParent = false; invalid.ZoneCode = "RECEIVE";
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { valid, invalid }, "FG01").Contains("STORAGE"), "non-storage source rejects whole merge");
            Pass();
        }

        private static void MergeBoxRulesRejectDimensionMismatch()
        {
            List<string> codes = new List<string> { "I001", "I002" };
            MergeBoxPackFact first = MergeFact(1, "I001", "H201", "1010", "FG01", "MAT-1", "41", 20m);
            MergeBoxPackFact second = MergeFact(2, "I002", "H208", "1010", "FG01", "MAT-2", "41", 30m);
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { first, second }, "FG01").Contains("物料编码"), "material mismatch rejected");
            second.MaterialCode = "MAT-1"; second.MaterialRevision = "42";
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { first, second }, "FG01").Contains("物料版本"), "revision mismatch rejected");
            second.MaterialRevision = "41"; second.PlantCode = "1020";
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { first, second }, "FG01").Contains("厂别"), "cross-plant rejected");
            second.PlantCode = "1010"; second.WarehouseCode = "FG02";
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { first, second }, "FG01").Contains("指定仓库"), "cross-warehouse rejected");
            second.WarehouseCode = "FG01"; second.MaterialCodeCount = 2;
            Equal(true, MergeBoxRules.Validate(codes, new List<MergeBoxPackFact> { first, second }, "FG01").StartsWith("INNER_PACK_ITEM_CONFLICT:"), "inner item conflict has stable code");
            Pass();
        }

        private static void MergeBoxServiceNormalizesAndPersists()
        {
            FakeRepackRepository repository = new FakeRepackRepository();
            RepackService service = new RepackService(new FakeHuRepository(), new FakeLocationMaintenanceRepository(), new FakeHuLocationRepository(), repository);
            OperationResult<MergeBoxResultDto> result = service.MergeBox(new MergeBoxRequest
            {
                WarehouseCode = " fg01 ", InnerPackHuCodes = new List<string> { " i001 ", " i002 " },
                RequestId = " REQ-MERGE ", BusinessNo = " BIZ-1 ", Operator = " tester ", Remark = " merge "
            });
            Equal(true, result.IsSuccess, "merge service succeeds");
            Equal("FG01", repository.LastMergeBoxCommand.WarehouseCode, "merge warehouse normalized");
            Equal("I001,I002", string.Join(",", repository.LastMergeBoxCommand.InnerPackHuCodes), "merge pack codes normalized");
            Equal("REQ-MERGE", repository.LastMergeBoxCommand.RequestId, "request id trimmed");
            Equal(false, service.MergeBox(new MergeBoxRequest { WarehouseCode = "FG01", InnerPackHuCodes = new List<string> { "I001", "i001" }, RequestId = "R", Operator = "u" }).IsSuccess, "duplicate request-list item rejected");
            Pass();
        }

        private static void MergeBoxServiceReplaysIdempotently()
        {
            FakeRepackRepository repository = new FakeRepackRepository
            {
                ExistingMerge = new MergeBoxResultDto
                {
                    NewBoxHuCode = "B2608260019A", WarehouseCode = "FG01", InnerPackHuCodes = new List<string> { "I001", "I002" }, IsDuplicate = false
                }
            };
            RepackService service = new RepackService(new FakeHuRepository(), new FakeLocationMaintenanceRepository(), new FakeHuLocationRepository(), repository);
            OperationResult<MergeBoxResultDto> replay = service.MergeBox(new MergeBoxRequest
            {
                WarehouseCode = "FG01", InnerPackHuCodes = new List<string> { "I002", "I001" }, RequestId = "REQ-19", Operator = "tester"
            });
            Equal(true, replay.IsSuccess, "same merge request replays");
            Equal("B2608260019A", replay.Data.NewBoxHuCode, "replay returns original box code");
            Equal(true, replay.Data.IsDuplicate, "replay marked duplicate");
            Equal(0, repository.MergeCalls, "replay consumes no sequence through execute");
            Equal(false, service.MergeBox(new MergeBoxRequest { WarehouseCode = "FG01", InnerPackHuCodes = new List<string> { "I003" }, RequestId = "REQ-19", Operator = "tester" }).IsSuccess, "request id conflict rejected");
            Pass();
        }

        private static void MergeBoxRepositoryIsAtomicAndConcurrentSafe()
        {
            string repository = File.ReadAllText(Path.Combine("WMS-HU-Model", "Repositories", "Impl", "MergeBoxRepository.cs"));
            string migration = File.ReadAllText(Path.Combine("docs", "database", "016_MergeBoxCodeSequence.sql"));
            Equal(true, repository.Contains("IsolationLevel.Serializable") && repository.Contains("WITH (UPDLOCK,HOLDLOCK)"), "merge and sequence allocation use serializable locking");
            Equal(true, repository.IndexOf("MergeBoxRules.Validate", StringComparison.Ordinal) < repository.IndexOf("AllocateSequence", StringComparison.Ordinal), "all pack rules run before sequence allocation");
            Equal(true, repository.Contains("BOX_CODE_DAILY_SEQUENCE_EXHAUSTED") && repository.Contains("current.Value >= 9999"), "sequence exhaustion is stable business failure");
            Equal(false, repository.Contains("MAX(HUCode)") || repository.Contains("AddWithValue"), "allocator avoids max HUCode and AddWithValue");
            Equal(true, repository.Contains("DELETE FROM dbo.WMS_HU_CurrentLocation") && repository.Contains("'INBOUND-PENDING'") && repository.Contains("'RECEIVE'"), "merge clears child direct locations and resolves pending target");
            Equal(true, repository.Contains("HuMoveLogWriter.Insert") && repository.Contains("\"MERGE_BOX\"") && repository.Contains("\"ATTACH_HU\""), "merge writes movement and operation audit");
            Equal(true, migration.Contains("PRIMARY KEY CLUSTERED (BusinessDate, PlantCode)") && migration.Contains("SET XACT_ABORT ON") && migration.Contains("BETWEEN 1 AND 9999"), "sequence migration is scoped and rerun-safe");
            Pass();
        }

        private static void MergeBoxPublicContractKeepsServerOwnedFieldsOutOfRequest()
        {
            string module = File.ReadAllText(Path.Combine("WMS", "Modules", "RepackModule.cs"));
            string models = File.ReadAllText(Path.Combine("WMS-HU-Model", "Models", "Repack", "MergeBoxModels.cs"));
            Type requestType = typeof(MergeBoxRequest);
            Equal(true, module.Contains("Post(\"/merge-box\"") && module.Contains("this.Bind<MergeBoxRequest>()"), "merge route uses typed binding");
            Equal(null, requestType.GetProperty("NewBoxHuCode"), "request cannot provide new box code");
            Equal(null, requestType.GetProperty("PlantCode"), "request cannot provide plant code");
            Equal(null, requestType.GetProperty("SequenceNo"), "request cannot provide sequence");
            Equal(null, requestType.GetProperty("TargetLocationCode"), "request cannot provide target location");
            Equal(true, models.Contains("NewBoxHuCode") && models.Contains("InnerPackHuCodes") && models.Contains("LocationCode"), "response exposes merge result contract");
            Pass();
        }

        private static PutawayRecommendationSnapshot RecommendationSnapshot(string locationSource)
        {
            PutawayRecommendationSnapshot snapshot = new PutawayRecommendationSnapshot
            {
                WarehouseAllowsPlant = true,
                Box = new PutawayRecommendationBoxSnapshot
                {
                    HuId = 1,
                    HuCode = "INCOMING-BOX",
                    HuType = "OUTER_BOX",
                    PlantCode = "1010",
                    LifecycleStatus = "ACTIVE",
                    IsHold = false,
                    EffectiveLocationId = 100,
                    EffectiveLocationCode = "INBOUND-PENDING",
                    EffectiveWarehouseCode = "FG01",
                    EffectiveZoneCode = "RECEIVE",
                    LocationSource = locationSource
                }
            };
            snapshot.IncomingItems.Add(new PutawayRecommendationItemSnapshot
            {
                MaterialCode = "MAT-A",
                MaterialRevision = "82",
                WeekYear = 2026,
                WeekNo = 34,
                WeekSortKey = 202634
            });
            return snapshot;
        }

        private static PutawayRecommendationLocationSnapshot RecommendationLocation(long id, string code, string rackGroup, string row, int? position)
        {
            return new PutawayRecommendationLocationSnapshot
            {
                LocationId = id,
                LocationCode = code,
                PlantPolicy = "SAME_PLANT",
                AllowMixedMaterial = false,
                PutawayPriority = 100,
                PhysicalRackGroupCode = rackGroup,
                PhysicalRowCode = row,
                PositionSequence = position
            };
        }

        private static void AddRecommendationBoxes(
            PutawayRecommendationSnapshot snapshot,
            long locationId,
            int count,
            string materialCode,
            string materialRevision,
            short weekYear,
            byte weekNo,
            long firstBoxId)
        {
            for (int index = 0; index < count; index++)
            {
                snapshot.Inventory.Add(new PutawayRecommendationInventorySnapshot
                {
                    BoxHuId = firstBoxId + index,
                    BoxHuCode = "BOX-" + (firstBoxId + index),
                    PlantCode = "1010",
                    LocationId = locationId,
                    MaterialCode = materialCode,
                    MaterialRevision = materialRevision,
                    WeekYear = weekYear,
                    WeekNo = weekNo,
                    WeekSortKey = weekYear * 100 + weekNo
                });
            }
        }

        private static OperationResult<PutawayRecommendationResultDto> Recommend(PutawayRecommendationSnapshot snapshot)
        {
            return new PutawayRecommendationService(new FakePutawayRecommendationRepository { Snapshot = snapshot })
                .GetRecommendations(" fg01 ", " incoming-box ");
        }

        private static InventoryItemFact Fact(long boxId, string boxCode, long packId, string packCode, string material, string revision, string batch, short year, byte week, decimal qty, bool hold)
        {
            return new InventoryItemFact
            {
                BoxHuId = boxId,
                BoxCode = boxCode,
                BoxStatus = "ACTIVE",
                BoxIsHold = hold,
                PackHuId = packId,
                PackCode = packCode,
                MaterialCode = material,
                MaterialRevision = revision,
                BatchNo = batch,
                WeekYear = year,
                WeekNo = week,
                WeekSortKey = year * 100 + week,
                Qty = qty
            };
        }

        private static MergeBoxPackFact MergeFact(long id, string code, string locationCode, string plantCode, string warehouseCode, string materialCode, string revision, decimal qty)
        {
            return new MergeBoxPackFact
            {
                HuId = id, HuCode = code, HuType = "INNER_PACK", PlantCode = plantCode, LifecycleStatus = "ACTIVE", IsHold = false,
                HasActiveParent = false, LocationId = id + 100, LocationCode = locationCode, WarehouseCode = warehouseCode,
                WarehouseStatus = "ACTIVE", ZoneCode = "STORAGE", ZoneStatus = "ACTIVE", LocationStatus = "ACTIVE", IsVirtual = false,
                ActiveItemCount = 1, MaterialCodeCount = 1, MaterialRevisionCount = 1, MaterialCode = materialCode, MaterialRevision = revision, TotalQty = qty
            };
        }

        private static HuIdentityDto Identity(long id, string code, string type)
        {
            return new HuIdentityDto { HuId = id, HuCode = code, HuType = type, MatchedHuCode = true };
        }

        private class FakePutawayRepository : IPutawayRepository
        {
            public PalletPutawayStatusDto Status;
            public WholePalletPutawayCommand LastWholeCommand;
            public PutawayCommand LastBoxCommand;
            public DirectReceivePutawayCommand LastDirectCommand;
            public PutawayResultDto Existing;
            public Action<DirectReceivePutawayCommand> OnDirectExecute;
            public int PalletBoxCalls;
            public int DirectCalls;
            public PalletPutawayStatusDto GetPalletStatus(long palletHuId) { return Status; }
            public PutawayResultDto GetByRequestId(string requestId) { return Existing; }
            public PutawayPersistenceResult ExecutePutaway(PutawayCommand command)
            {
                PalletBoxCalls++;
                LastBoxCommand = command;
                return PutawayPersistenceResult.Success(new PutawayResultDto
                {
                    SourceMode = "PALLET_INHERITED", PalletHuCode = command.PalletHuCode, BoxHuCode = command.BoxHuCode,
                    FromLocationCode = command.FromLocationCode, ToLocationCode = command.ToLocationCode, RemainingBoxCount = 1
                });
            }
            public PutawayPersistenceResult ExecuteDirectReceivePutaway(DirectReceivePutawayCommand command)
            {
                DirectCalls++;
                LastDirectCommand = command;
                if (OnDirectExecute != null) OnDirectExecute(command);
                return PutawayPersistenceResult.Success(new PutawayResultDto
                {
                    SourceMode = "DIRECT_RECEIVE", BoxHuCode = command.BoxHuCode, FromLocationCode = command.FromLocationCode,
                    ToLocationCode = command.ToLocationCode, RemainingBoxCount = 0
                });
            }
            public WholePalletPutawayResultDto GetWholePalletPutawayByRequestId(string requestId) { return null; }
            public WholePalletPutawayPersistenceResult ExecuteWholePalletPutaway(WholePalletPutawayCommand command)
            {
                LastWholeCommand = command;
                return WholePalletPutawayPersistenceResult.Success(new WholePalletPutawayResultDto { PalletHuCode = command.PalletHuCode, ToLocationCode = command.TargetLocationCode, MovedCount = 1 });
            }
        }

        private class FakePutawayRecommendationRepository : IPutawayRecommendationRepository
        {
            public PutawayRecommendationSnapshot Snapshot;
            public PutawayRecommendationSnapshot LoadSnapshot(string warehouseCode, string boxHuCode)
            {
                Equal("FG01", warehouseCode, "recommendation warehouse normalized");
                Equal("INCOMING-BOX", boxHuCode, "recommendation box normalized");
                return Snapshot;
            }
        }

        private class DirectPutawayFixture
        {
            public FakeHuRepository Hus = new FakeHuRepository();
            public FakeHuLocationRepository HuLocations = new FakeHuLocationRepository();
            public FakePutawayRepository Repository = new FakePutawayRepository();
            public FakeEffectiveLocationResolver Resolver = new FakeEffectiveLocationResolver();
            public FakeLocationMaintenanceRepository Locations;
            public PutawayService Service;
        }

        private class FakeRelocationRepository : IRelocationRepository
        {
            public BatchRelocationCommand LastCommand;
            public BatchRelocationResultDto Existing;
            public BatchRelocationPersistenceResult Next;
            public BatchRelocationResultDto GetByRequestId(string requestId) { return Existing; }
            public BatchRelocationPersistenceResult ExecuteBatch(BatchRelocationCommand command)
            {
                LastCommand = command;
                return Next ?? BatchRelocationPersistenceResult.Success(new BatchRelocationResultDto { SourceLocationCode = command.SourceLocationCode, TargetLocationCode = command.TargetLocationCode, BoxCodes = command.BoxHuCodes, MovedCount = command.BoxHuCodes.Count });
            }
        }

        private class FakeRepackRepository : IRepackRepository
        {
            public UnpackBoxCommand LastUnpackBoxCommand;
            public MergeBoxCommand LastMergeBoxCommand;
            public MergeBoxResultDto ExistingMerge;
            public int MergeCalls;
            public BoxRepackStatusDto GetBoxStatus(long boxHuId) { return null; }
            public TransferInnerResultDto GetTransferByRequestId(string requestId) { return null; }
            public TransferInnerPersistenceResult ExecuteTransferInner(TransferInnerCommand command) { throw new NotSupportedException(); }
            public UnpackInnerResultDto GetUnpackByRequestId(string requestId) { return null; }
            public UnpackInnerPersistenceResult ExecuteUnpackInner(UnpackInnerCommand command) { throw new NotSupportedException(); }
            public PackInnerResultDto GetPackByRequestId(string requestId) { return null; }
            public PackInnerPersistenceResult ExecutePackInner(PackInnerCommand command) { throw new NotSupportedException(); }
            public UnpackBoxResultDto GetUnpackBoxByRequestId(string requestId) { return null; }
            public UnpackBoxPersistenceResult ExecuteUnpackBox(UnpackBoxCommand command)
            {
                LastUnpackBoxCommand = command;
                return UnpackBoxPersistenceResult.Success(new UnpackBoxResultDto { BoxHuCode = command.BoxHuCode, ToLocationCode = command.ToLocationCode, UnpackedCount = 2 });
            }
            public MergeBoxResultDto GetMergeBoxByRequestId(string requestId) { return ExistingMerge; }
            public MergeBoxPersistenceResult ExecuteMergeBox(MergeBoxCommand command)
            {
                MergeCalls++;
                LastMergeBoxCommand = command;
                return MergeBoxPersistenceResult.Success(new MergeBoxResultDto
                {
                    NewBoxHuCode = "B2608260001A", PlantCode = "1010", WarehouseCode = command.WarehouseCode,
                    InnerPackHuCodes = new List<string>(command.InnerPackHuCodes), InnerPackCount = command.InnerPackHuCodes.Count,
                    LocationCode = "INBOUND-PENDING", ZoneCode = "RECEIVE"
                });
            }
        }

        private class FakeFormalOutboundRepository : IFormalOutboundRepository
        {
            public OutboundPickingStateDto PickingState;
            public string PickingStateError;
            public int WriteCalls;
            public List<OutboundOrderDto> GetOrders(OutboundOrderQuery query) { return new List<OutboundOrderDto>(); }
            public OutboundOrderDto GetOrder(string sapDeliveryNo) { return null; }
            public List<OutboundAllocationCandidateDto> GetAllocationCandidates(OutboundOrderDto order, string materialCode, string uom) { return new List<OutboundAllocationCandidateDto>(); }
            public StartPickingResultDto GetStartPickingByRequestId(string requestId) { return null; }
            public StartPickingPersistenceResult ExecuteStartPicking(StartPickingCommand command) { WriteCalls++; throw new NotSupportedException(); }
            public OutboundActionPersistenceResult ReleaseAllocation(string allocationNo, OutboundActionRequest request) { WriteCalls++; throw new NotSupportedException(); }
            public OutboundActionPersistenceResult VoidOrder(string sapDeliveryNo, OutboundActionRequest request) { WriteCalls++; throw new NotSupportedException(); }
            public string GetPickBatchCustomerCode(string pickBatchNo) { return null; }
            public OutboundScanResultDto GetScanByRequestId(string requestId) { return null; }
            public OutboundScanPersistenceResult ExecuteScan(OutboundScanCommand command) { WriteCalls++; throw new NotSupportedException(); }
            public OutboundActionPersistenceResult ShipOrder(string sapDeliveryNo, ShipOutboundRequest request) { WriteCalls++; throw new NotSupportedException(); }
            public OutboundActionPersistenceResult ReturnPickDetail(long pickDetailId, ReturnPickDetailRequest request) { WriteCalls++; throw new NotSupportedException(); }
            public OutboundActionPersistenceResult ReturnToStock(string huCode, ReturnToStockRequest request) { WriteCalls++; throw new NotSupportedException(); }
            public StartPickingPersistenceResult ReserveReplacement(string pickBatchNo, ReplacementRequest request) { WriteCalls++; throw new NotSupportedException(); }
            public OutboundPickingStateReadResult GetPickingState(string sapDeliveryNo) { return PickingStateError == null ? OutboundPickingStateReadResult.Success(PickingState) : OutboundPickingStateReadResult.Fail(PickingStateError); }
        }

        private class FakeInventoryQueryRepository : IInventoryQueryRepository
        {
            public Dictionary<string, InventoryLookupLocationRecord> Locations = new Dictionary<string, InventoryLookupLocationRecord>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, InventoryLookupLocationRecord> ForeignLocations = new Dictionary<string, InventoryLookupLocationRecord>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<long, HuDto> Hus = new Dictionary<long, HuDto>();
            public List<InventoryItemFact> LocationFacts = new List<InventoryItemFact>();
            public Dictionary<long, List<InventoryItemFact>> OuterFacts = new Dictionary<long, List<InventoryItemFact>>();
            public Dictionary<long, List<InventoryItemFact>> InnerFacts = new Dictionary<long, List<InventoryItemFact>>();
            public Dictionary<long, string> ParentBoxes = new Dictionary<long, string>();
            public InventoryLookupLocationRecord GetLocation(string warehouseCode, string locationCode) { InventoryLookupLocationRecord value; return Locations.TryGetValue(warehouseCode + "|" + locationCode, out value) ? value : null; }
            public InventoryLookupLocationRecord FindLocation(string locationCode) { InventoryLookupLocationRecord value; return ForeignLocations.TryGetValue(locationCode, out value) ? value : null; }
            public HuDto GetHu(long huId) { HuDto value; return Hus.TryGetValue(huId, out value) ? value : null; }
            public List<InventoryItemFact> GetLocationInventoryFacts(IList<long> boxHuIds) { return LocationFacts.Where(x => boxHuIds.Contains(x.BoxHuId)).ToList(); }
            public List<InventoryItemFact> GetOuterBoxFacts(long boxHuId) { List<InventoryItemFact> value; return OuterFacts.TryGetValue(boxHuId, out value) ? value : new List<InventoryItemFact>(); }
            public List<InventoryItemFact> GetInnerPackFacts(long packHuId) { List<InventoryItemFact> value; return InnerFacts.TryGetValue(packHuId, out value) ? value : new List<InventoryItemFact>(); }
            public string GetActiveParentBoxCode(long packHuId) { string value; return ParentBoxes.TryGetValue(packHuId, out value) ? value : null; }
        }

        private class FakeHuIdentifierService : IHuIdentifierService
        {
            public Dictionary<string, HuIdentityDto> Results = new Dictionary<string, HuIdentityDto>(StringComparer.OrdinalIgnoreCase);
            public OperationResult<HuIdentityDto> Resolve(string rawValue, string customerCode)
            {
                HuIdentityDto value;
                return Results.TryGetValue(rawValue, out value)
                    ? OperationResult<HuIdentityDto>.Success(value)
                    : OperationResult<HuIdentityDto>.Fail("箱码未匹配到有效HU。");
            }
        }

        private class FakeEffectiveLocationResolver : IEffectiveLocationResolver
        {
            public Dictionary<long, HuLocationDto> Locations = new Dictionary<long, HuLocationDto>();
            public Dictionary<long, List<long>> HuIdsByLocation = new Dictionary<long, List<long>>();
            public string LastFindHuType;
            public HuLocationDto Resolve(long huId) { HuLocationDto value; return Locations.TryGetValue(huId, out value) ? value : null; }
            public List<HuLocationDto> ResolveMany(IList<long> huIds) { return huIds.Select(Resolve).Where(x => x != null).ToList(); }
            public List<long> FindHuIdsAtLocation(long locationId, string huType) { LastFindHuType = huType; List<long> value; return HuIdsByLocation.TryGetValue(locationId, out value) ? value : new List<long>(); }
        }

        private static void HuDetailElevatesRootAndPreservesLocationSemantics()
        {
            FakeHuRepository huRepository = HuHierarchy();
            FakeHuLocationService locationService = new FakeHuLocationService();
            locationService.Locations["PACK-1"] = new HuLocationDto
            {
                HuCode = "PACK-1",
                DirectLocationCode = null,
                EffectiveLocationCode = "A-01-01",
                EffectiveLocationName = "成品货架 A-01-01",
                LocationSourceHuCode = "PALLET-1",
                IsInherited = true
            };
            FakeHuDetailRepository detailRepository = new FakeHuDetailRepository();
            detailRepository.Contents.Add(new HuDetailContentDto { HuId = 3, HuCode = "PACK-1", HuType = "INNER_PACK", Qty = 10m });
            detailRepository.ActiveHold = new HuHoldLogDto { HoldNo = "HOLD-1", HuCode = "PACK-1", IsActive = true };
            HuDetailService service = new HuDetailService(huRepository, locationService, detailRepository);

            OperationResult<HuDetailDto> result = service.GetDetail(" pack-1 ");

            Equal(true, result.IsSuccess, "HU detail succeeds");
            Equal("PALLET-1", result.Data.RootHuCode, "HU detail elevates to current top ancestor");
            Equal("BOX-1", result.Data.Parent.HuCode, "HU detail current active parent");
            Equal(3, result.Data.Tree.Count, "HU detail current three-level tree");
            Equal(true, result.Data.Location.IsInherited, "HU detail preserves inherited location");
            Equal("A-01-01", result.Data.Location.EffectiveLocationCode, "HU detail effective location");
            Equal("成品货架 A-01-01", result.Data.Location.EffectiveLocationName, "HU detail effective location name");
            Equal(null, result.Data.Location.DirectLocationCode, "HU detail direct location remains explicit null");
            Equal("HOLD-1", result.Data.CurrentHold.HoldNo, "HU detail active hold");
            Equal("INNER_PACK", detailRepository.LastContentHuType, "HU detail content query follows selected HU type");
            locationService.Locations["PALLET-1"] = new HuLocationDto { HuCode = "PALLET-1", DirectLocationCode = "A-01-01", EffectiveLocationCode = "A-01-01", IsInherited = false };
            HuDetailDto direct = service.GetDetail("PALLET-1").Data;
            Equal("A-01-01", direct.Location.DirectLocationCode, "HU detail direct location");
            Equal(false, direct.Location.IsInherited, "HU detail direct location is not inherited");
            locationService.Locations["BOX-1"] = new HuLocationDto { HuCode = "BOX-1" };
            HuDetailDto noLocation = service.GetDetail("BOX-1").Data;
            Equal(null, noLocation.Location.DirectLocationCode, "HU detail no direct location");
            Equal(null, noLocation.Location.EffectiveLocationCode, "HU detail no effective location");
            Pass();
        }

        private static void HuDetailSupportsShippedAndUnknownHu()
        {
            FakeHuRepository repository = HuHierarchy();
            repository.Hus["BOX-1"].LifecycleStatus = "SHIPPED";
            FakeHuLocationService location = new FakeHuLocationService();
            location.Locations["BOX-1"] = new HuLocationDto { HuCode = "BOX-1", EffectiveLocationCode = "SHIPPED" };
            HuDetailService service = new HuDetailService(repository, location, new FakeHuDetailRepository());

            Equal(true, service.GetDetail("BOX-1").IsSuccess, "shipped HU remains queryable");
            Equal("SHIPPED", service.GetDetail("BOX-1").Data.Hu.LifecycleStatus, "shipped lifecycle preserved");
            Equal(false, service.GetDetail("MISSING").IsSuccess, "unknown HU fails");
            Pass();
        }

        private static void HuDetailHistoryIsBoundedAndOrdered()
        {
            FakeHuRepository repository = HuHierarchy();
            FakeHuDetailRepository detail = new FakeHuDetailRepository();
            HuDetailService service = new HuDetailService(repository, new FakeHuLocationService(), detail);

            service.GetOperationLogs("BOX-1", 999);
            Equal(200, detail.LastOperationTop, "operation history capped");
            service.GetHoldLogs("BOX-1", 0);
            Equal(50, detail.LastHoldTop, "hold history default");
            detail.HoldLogs.Add(new HuHoldLogDto { HoldNo = "HOLD-RELEASED", IsActive = false, ReleaseTime = new DateTime(2026, 8, 22), ReleaseRequestId = "REL-1" });
            List<HuHoldLogDto> released = service.GetHoldLogs("BOX-1", 50).Data;
            Equal(false, released.Single().IsActive, "released hold history retained");
            Equal("REL-1", released.Single().ReleaseRequestId, "released hold request id retained");
            string operationsSql = HuDetailRepository.OperationLogsSql.ToUpperInvariant();
            string holdsSql = HuDetailRepository.HoldLogsSql.ToUpperInvariant();
            string movesSql = HuLocationRepository.MoveLogsSql.ToUpperInvariant();
            Equal(true, operationsSql.Contains("WHERE LOG.HUID = @HUID") && operationsSql.Contains("ORDER BY LOG.OPERATIONTIME DESC, LOG.ID DESC"), "operation history scoped and newest first");
            Equal(true, holdsSql.Contains("WHERE LOG.HUID = @HUID") && holdsSql.Contains("ORDER BY LOG.HOLDTIME DESC, LOG.ID DESC"), "hold history scoped and newest first");
            Equal(true, movesSql.Contains("WHERE M.HUID = @HUID") && movesSql.Contains("ORDER BY M.MOVETIME DESC, M.ID DESC"), "move history reuses scoped newest-first SQL");
            Pass();
        }

        private static void FinishedGoodsInboundReadQueryValidatesAndNormalizes()
        {
            FakeFinishedGoodsInboundReadRepository repository = new FakeFinishedGoodsInboundReadRepository();
            repository.QueryResult.Items.Add(new FinishedGoodsInboundRecordDto { ReceiptId = 1, ExceptionBoxCount = 2 });
            FinishedGoodsInboundReadService service = new FinishedGoodsInboundReadService(repository, new FakeHuLocationRepository());
            OperationResult<FinishedGoodsInboundRecordResultDto> result = service.Query(new FinishedGoodsInboundRecordQuery
            {
                DateFrom = "2026-08-01",
                DateTo = "2026-08-22",
                PlantCode = " 1010 ",
                SourceSystem = " mes ",
                SourceType = " plan_stock ",
                SourceEntityId = " source-1 ",
                PalletCode = " plt-1 ",
                BoxCode = " box-1 ",
                MaterialCode = " mat-1 ",
                RequestId = " request-1 ",
                ExceptionCategory = " weekcode ",
                ExceptionOnly = true
            });

            Equal(true, result.IsSuccess, "inbound read query succeeds");
            Equal(1, repository.LastQuery.Page, "inbound read default page");
            Equal(50, repository.LastQuery.PageSize, "inbound read default page size");
            Equal(new DateTime(2026, 8, 1), repository.LastQuery.FromTime.Value, "inbound read inclusive start date");
            Equal(new DateTime(2026, 8, 23), repository.LastQuery.ToTimeExclusive.Value, "inbound read exclusive end date");
            Equal("1010", repository.LastQuery.PlantCode, "inbound read plant normalized");
            Equal("MES", repository.LastQuery.SourceSystem, "inbound read source normalized");
            Equal("PLAN_STOCK", repository.LastQuery.SourceType, "inbound read source type normalized");
            Equal("source-1", repository.LastQuery.SourceEntityId, "inbound read source entity preserves case");
            Equal("PLT-1", repository.LastQuery.PalletCode, "inbound read pallet normalized");
            Equal("BOX-1", repository.LastQuery.BoxCode, "inbound read box normalized");
            Equal("MAT-1", repository.LastQuery.MaterialCode, "inbound read material normalized");
            Equal("request-1", repository.LastQuery.RequestId, "inbound read request id preserves case");
            Equal("WEEKCODE", repository.LastQuery.ExceptionCategory, "inbound read exception category normalized");
            Equal("WITH_EXCEPTION", result.Data.Items.Single().DisplayStatus, "inbound read display status derived");
            Equal(false, service.Query(new FinishedGoodsInboundRecordQuery { Page = -1 }).IsSuccess, "inbound read rejects invalid page");
            Equal(false, service.Query(new FinishedGoodsInboundRecordQuery { PageSize = 201 }).IsSuccess, "inbound read rejects oversized page");
            Equal(false, service.Query(new FinishedGoodsInboundRecordQuery { DateFrom = "2026/08/01" }).IsSuccess, "inbound read rejects invalid date");
            Equal(false, service.Query(new FinishedGoodsInboundRecordQuery { ExceptionCategory = "REJECTED" }).IsSuccess, "inbound read rejects unsupported exception category");
            Pass();
        }

        private static void FinishedGoodsInboundReadSqlAggregatesByReceipt()
        {
            string sql = FinishedGoodsInboundReadRepository.ReceiptStatsSql.ToUpperInvariant();
            string rowsSql = FinishedGoodsInboundReadRepository.RowsSql.ToUpperInvariant();
            string countSql = FinishedGoodsInboundReadRepository.CountSql.ToUpperInvariant();
            Equal(true, sql.Contains("FROM DBO.WMS_FINISHEDGOODSINBOUNDRECEIPT RECEIPT"), "inbound read grain starts at receipt");
            Equal(true, sql.Contains("'FGI-' + CAST(RECEIPT.ID AS VARCHAR(20))"), "inbound read links receipt source business number");
            Equal(true, sql.Contains("COUNT(CASE WHEN HU.HUTYPE = 'OUTER_BOX' THEN 1 END)") && sql.Contains("COUNT(CASE WHEN HU.HUTYPE = 'INNER_PACK' THEN 1 END)"), "inbound read counts receipt-created boxes and packs");
            Equal(true, sql.Contains("SUM(ITEM.QTY)") && sql.Contains("COUNT(DISTINCT ITEM.MATERIALCODE)"), "inbound read aggregates quantity and materials");
            Equal(true, sql.Contains("WMS_HU_HOLDLOG") && sql.Contains("WEEKCODE_UNRESOLVED") && sql.Contains("WEEKHOLD.ISACTIVE = 1"), "inbound read aggregates active holds and week exceptions");
            Equal(true, sql.Contains("@FROMTIME") && sql.Contains("@TOTIMEEXCLUSIVE") && sql.Contains("@PLANTCODE") && sql.Contains("@SOURCESYSTEM") && sql.Contains("@SOURCETYPE") && sql.Contains("@SOURCEENTITYID"), "inbound read header filters are server side");
            Equal(true, sql.Contains("@PALLETCODE") && sql.Contains("@BOXCODE") && sql.Contains("@MATERIALCODE") && sql.Contains("@REQUESTID") && rowsSql.Contains("@EXCEPTIONONLY") && rowsSql.Contains("@EXCEPTIONCATEGORY"), "inbound read HU and exception filters are server side");
            Equal(true, rowsSql.Contains("ORDER BY RECEIVETIME DESC, RECEIPTID DESC") && rowsSql.Contains("OFFSET @OFFSET ROWS FETCH NEXT @PAGESIZE ROWS ONLY"), "inbound read paging is stable and server side");
            Equal(true, countSql.Contains("SELECT COUNT(1)") && countSql.Contains("FROM RECEIPTSTATS"), "inbound read total counts receipt rows");
            Pass();
        }

        private static void FinishedGoodsInboundReadDetailUsesBatchLocations()
        {
            FakeFinishedGoodsInboundReadRepository repository = new FakeFinishedGoodsInboundReadRepository();
            repository.Detail = new FinishedGoodsInboundDetailDto
            {
                Record = new FinishedGoodsInboundRecordDto { ReceiptId = 7, BoxCount = 3 },
                Pallets = new List<FinishedGoodsInboundPalletDto>(),
                Boxes = new List<FinishedGoodsInboundBoxDto>
                {
                    new FinishedGoodsInboundBoxDto { HuId = 21, BoxCode = "BOX-21" },
                    new FinishedGoodsInboundBoxDto { HuId = 22, BoxCode = "BOX-22" }
                }
            };
            FakeHuLocationRepository locations = new FakeHuLocationRepository();
            locations.Locations.Add(new HuLocationDto { HuId = 21, EffectiveLocationCode = "A-01", IsInherited = true, LocationSourceHuCode = "PLT-1" });
            locations.Locations.Add(new HuLocationDto { HuId = 22, DirectLocationCode = "B-01", EffectiveLocationCode = "B-01" });
            FinishedGoodsInboundReadService service = new FinishedGoodsInboundReadService(repository, locations);

            OperationResult<FinishedGoodsInboundDetailDto> result = service.GetDetail(7);

            Equal(true, result.IsSuccess, "inbound read detail succeeds without pallet");
            Equal(50, repository.LastBoxLimit, "inbound read detail box limit");
            Equal("21,22", string.Join(",", locations.LastHuIds), "inbound read detail resolves locations in one batch");
            Equal(1, locations.BatchCalls, "inbound read detail makes one location call");
            Equal("A-01", result.Data.Boxes[0].EffectiveLocationCode, "inbound read inherited location mapped");
            Equal("PLT-1", result.Data.Boxes[0].LocationSourceHuCode, "inbound read location source mapped");
            Equal(true, result.Data.HasMoreBoxes, "inbound read detail marks truncated boxes");
            repository.Detail = null;
            Equal(false, service.GetDetail(999).IsSuccess, "inbound read unknown receipt fails");
            Pass();
        }

        private class FakeFinishedGoodsInboundReadRepository : IFinishedGoodsInboundReadRepository
        {
            public FinishedGoodsInboundRecordQuery LastQuery;
            public int LastBoxLimit;
            public FinishedGoodsInboundRecordResultDto QueryResult = new FinishedGoodsInboundRecordResultDto { Items = new List<FinishedGoodsInboundRecordDto>() };
            public FinishedGoodsInboundDetailDto Detail;
            public FinishedGoodsInboundRecordResultDto Query(FinishedGoodsInboundRecordQuery query) { LastQuery = query; return QueryResult; }
            public FinishedGoodsInboundDetailDto GetDetail(long receiptId, int boxLimit) { LastBoxLimit = boxLimit; return Detail; }
        }

        private class FakeHuLocationRepository : IHuLocationRepository
        {
            public int BatchCalls;
            public List<long> LastHuIds = new List<long>();
            public List<HuLocationDto> Locations = new List<HuLocationDto>();
            public HuLocationDto GetLocation(long huId) { return Locations.FirstOrDefault(x => x.HuId == huId); }
            public List<HuLocationDto> GetLocations(IList<long> huIds) { BatchCalls++; LastHuIds = huIds.ToList(); return Locations.Where(x => huIds.Contains(x.HuId)).ToList(); }
            public HuMoveLogDto GetMoveByRequestId(string requestId) { return null; }
            public bool WarehouseAllowsPlant(string warehouseCode, string plantCode) { return true; }
            public int CountDifferentPlantAtLocation(long locationId, string plantCode) { return 0; }
            public MovePersistenceResult ExecuteMove(HuMoveCommand command) { throw new NotSupportedException(); }
            public List<HuMoveLogDto> GetMoveLogs(long huId, int top) { return new List<HuMoveLogDto>(); }
        }

        private static void MasterDataWarehouseLifecycleIsGuarded()
        {
            FakeMasterDataMaintenanceRepository repository = new FakeMasterDataMaintenanceRepository();
            repository.Warehouses["FG01"] = new WarehouseDto { Id = 1, WarehouseCode = "FG01", WarehouseName = "成品仓", WarehouseType = "FINISHED_GOODS", IsActive = true };
            MasterDataMaintenanceService service = new MasterDataMaintenanceService(repository);
            Equal(false, service.CreateWarehouse(new WarehouseCreateRequest { WarehouseCode = "FG01", WarehouseName = "重复", WarehouseType = "FINISHED_GOODS", Operator = "tester" }).IsSuccess, "warehouse duplicate rejected");
            Equal(false, service.CreateWarehouse(new WarehouseCreateRequest { WarehouseCode = "NEW", WarehouseName = "New", WarehouseType = "ARBITRARY", Operator = "tester" }).IsSuccess, "warehouse type allowlist enforced");
            Equal(false, service.CreateWarehouse(new WarehouseCreateRequest { WarehouseCode = "NEW", WarehouseName = "New", WarehouseType = "FINISHED_GOODS", Remark = new string('x', 1001), Operator = "tester" }).IsSuccess, "warehouse remark length enforced");
            Equal(true, service.CreateWarehouse(new WarehouseCreateRequest { WarehouseCode = " new ", WarehouseName = "New", WarehouseType = "finished_goods", Operator = "tester" }).IsSuccess, "warehouse create succeeds");
            Equal("NEW", repository.LastCreatedWarehouseCode, "warehouse create normalizes code");
            Equal(true, service.UpdateWarehouse("NEW", new WarehouseUpdateRequest { WarehouseName = "Renamed", WarehouseType = "FINISHED_GOODS", Operator = "tester" }).IsSuccess, "warehouse update succeeds");
            Equal("NEW", repository.LastUpdatedWarehouseCode, "warehouse natural key immutable on update");
            repository.HasActiveLocationValue = true;
            Equal(false, service.SetWarehouseActive("NEW", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "warehouse active location blocks disable");
            repository.HasActiveLocationValue = false; repository.HasCurrentHuValue = true;
            Equal(false, service.SetWarehouseActive("NEW", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "warehouse current HU blocks disable");
            repository.HasCurrentHuValue = false; repository.HasActiveMappingValue = false;
            Equal(true, service.SetWarehouseActive("NEW", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "empty warehouse disables");
            Equal(true, service.SetWarehouseActive("NEW", true, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "warehouse enables");
            MasterDataOptionsDto options = service.GetOptions("FG01");
            Equal("DOUBLE_RACK,TRIPLE_TIER_REMAINDER_RACK,FLOOR_STORAGE", string.Join(",", options.LocationTypes), "server owns approved physical location types");
            Equal(false, options.LocationTypes.Contains("VIRTUAL"), "system location type excluded from supervisor options");
            Pass();
        }

        private static void MasterDataWarehousePlantLifecycleIsGuarded()
        {
            FakeMasterDataMaintenanceRepository repository = new FakeMasterDataMaintenanceRepository();
            repository.Warehouses["FG01"] = new WarehouseDto { Id = 1, WarehouseCode = "FG01", WarehouseName = "FG", WarehouseType = "FINISHED_GOODS", IsActive = true };
            repository.Warehouses["OFF"] = new WarehouseDto { Id = 2, WarehouseCode = "OFF", WarehouseName = "Off", WarehouseType = "FINISHED_GOODS", IsActive = false };
            MasterDataMaintenanceService service = new MasterDataMaintenanceService(repository);
            Equal(false, service.CreateWarehousePlant(new WarehousePlantCreateRequest { WarehouseCode = "OFF", PlantCode = "1010", Operator = "tester" }).IsSuccess, "inactive warehouse mapping rejected");
            Equal(false, service.CreateWarehousePlant(new WarehousePlantCreateRequest { WarehouseCode = "FG01", PlantCode = "1010", Remark = new string('x', 1001), Operator = "tester" }).IsSuccess, "mapping remark length enforced");
            Equal(true, service.CreateWarehousePlant(new WarehousePlantCreateRequest { WarehouseCode = "FG01", PlantCode = "9010", SapStorageLocation = null, Operator = "tester" }).IsSuccess, "nullable SAP location accepted");
            Equal(null, repository.Mappings["FG01|9010"].SapStorageLocation, "nullable SAP location persisted");
            Equal(false, service.CreateWarehousePlant(new WarehousePlantCreateRequest { WarehouseCode = "FG01", PlantCode = "9010", Operator = "tester" }).IsSuccess, "duplicate warehouse plant rejected");
            Equal(true, service.UpdateWarehousePlant("FG01", "9010", new WarehousePlantUpdateRequest { SapStorageLocation = "11C3", Operator = "tester" }).IsSuccess, "SAP location update succeeds");
            Equal("11C3", repository.Mappings["FG01|9010"].SapStorageLocation, "SAP location changed");
            Equal(true, service.SetWarehousePlantActive("FG01", "9010", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "mapping disables without deleting");
            repository.Warehouses["FG01"].IsActive = false;
            Equal(false, service.SetWarehousePlantActive("FG01", "9010", true, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "mapping cannot enable under inactive warehouse");
            Pass();
        }

        private static void MasterDataLocationLifecycleIsGuarded()
        {
            FakeLocationMaintenanceRepository repository = new FakeLocationMaintenanceRepository();
            repository.WarehouseActive = true;
            LocationService service = new LocationService(repository);
            CreateLocationRequest create = new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = "A-01", LocationTypeCode = "DOUBLE_RACK", Operator = "tester" };
            Equal(true, service.Create(create).IsSuccess, "location create succeeds");
            Equal(false, service.Create(new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "SYSTEM", LocationCode = "SHIPPED", LocationTypeCode = "DOUBLE_RACK", Operator = "tester" }).IsSuccess, "reserved location create rejected");
            repository.CurrentHuCount = 1;
            OperationResult<LocationDto> occupiedDisable = service.SetActive("FG01", "A-01", false, new MasterDataStatusRequest { Operator = "tester" });
            Equal(false, occupiedDisable.IsSuccess, "location current HU blocks disable");
            Equal("当前货位仍有库存，请先完成移货，确认货位为空后再下线。", occupiedDisable.Message, "occupied location returns actionable offline guidance");
            repository.CurrentHuCount = 0;
            repository.OccupyOnStatusUpdate = true;
            Equal(false, service.SetActive("FG01", "A-01", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "execution-time occupancy race blocks disable");
            repository.OccupyOnStatusUpdate = false;
            Equal(true, service.SetActive("FG01", "A-01", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "empty location disables");
            Equal(true, repository.Locations.ContainsKey("A-01"), "disable preserves location master record");
            Equal("INACTIVE", repository.Locations["A-01"].MasterStatus, "disable changes status only");
            Equal(true, service.SetActive("FG01", "A-01", true, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "disabled physical location can be enabled again");
            Equal("ACTIVE", repository.Locations["A-01"].MasterStatus, "reenable retains the same location master record");
            repository.Locations["INBOUND-PENDING"] = new LocationDto { Id = 2, WarehouseCode = "FG01", LocationCode = "INBOUND-PENDING", ZoneCode = "RECEIVE", LocationTypeCode = "VIRTUAL", MasterStatus = "ACTIVE" };
            Equal(false, service.SetActive("FG01", "INBOUND-PENDING", false, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "system location disable rejected");
            Equal(false, service.Update("FG01", "INBOUND-PENDING", LocationUpdate("tester")).IsSuccess, "system location edit rejected");
            repository.WarehouseActive = false;
            Equal(false, service.Create(new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = "A-02", LocationTypeCode = "DOUBLE_RACK", Operator = "tester" }).IsSuccess, "inactive warehouse location rejected");
            Pass();
        }

        private static void MasterDataLocationControlledMaintenanceIsEnforced()
        {
            FakeLocationMaintenanceRepository repository = new FakeLocationMaintenanceRepository { WarehouseActive = true };
            LocationService service = new LocationService(repository);
            CreateLocationRequest triple = new CreateLocationRequest
            {
                WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = " 01w ",
                LocationTypeCode = "triple_tier_remainder_rack", CapacityQty = 16, Operator = "tester"
            };
            Equal(true, service.Create(triple).IsSuccess, "approved triple-tier type accepted");
            Equal(true, repository.Locations.ContainsKey("01w"), "location code is trim-only without uppercase rewrite");
            Equal(null, triple.CapacityQty, "create capacity is server-deferred");
            Equal(true, service.Create(new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = "F001", LocationTypeCode = "FLOOR_STORAGE", Operator = "tester" }).IsSuccess, "approved floor type accepted");
            Equal(false, service.Create(new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = "V001", LocationTypeCode = "VIRTUAL", Operator = "tester" }).IsSuccess, "virtual physical create rejected");
            Equal(false, service.Create(new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "STORAGE", LocationCode = "X001", LocationTypeCode = "UNKNOWN", Operator = "tester" }).IsSuccess, "invalid physical type rejected");
            Equal(false, service.Create(new CreateLocationRequest { WarehouseCode = "FG01", ZoneCode = "RETURN", LocationCode = "X002", LocationTypeCode = "DOUBLE_RACK", Operator = "tester" }).IsSuccess, "non-storage physical create rejected");
            repository.Locations["SYS-X"] = new LocationDto { Id = 20, WarehouseCode = "FG01", ZoneCode = "SYSTEM", LocationCode = "SYS-X", LocationTypeCode = "VIRTUAL", IsVirtual = true, MasterStatus = "ACTIVE" };
            Equal(true, service.Get("FG01", "SYS-X").Data.IsSystemProtected, "virtual location returned as system protected");
            Equal(false, service.Update("FG01", "SYS-X", LocationUpdate("tester")).IsSuccess, "virtual system update rejected");
            Equal(false, service.SetActive("FG01", "SYS-X", true, new MasterDataStatusRequest { Operator = "tester" }).IsSuccess, "virtual system enable rejected");
            Equal(false, service.Update("FG01", "F001", new UpdateLocationRequest { ZoneCode = "RETURN", LocationTypeCode = "DOUBLE_RACK", Operator = "tester" }).IsSuccess, "physical update outside storage rejected");
            Pass();
        }

        private static void MasterDataSqlContractsAreSetBasedAndTyped()
        {
            string warehouseSql = MasterDataMaintenanceRepository.WarehouseQuerySql.ToUpperInvariant();
            string mappingSql = MasterDataMaintenanceRepository.WarehousePlantQuerySql.ToUpperInvariant();
            string locationSql = LocationRepository.MaintenanceListSql.ToUpperInvariant();
            Equal(true, warehouseSql.Contains("WMS_HU_CURRENTLOCATION") && warehouseSql.Contains("ACTIVELOCATIONCOUNT") && warehouseSql.Contains("ACTIVEMAPPINGCOUNT"), "warehouse usage aggregated in query");
            Equal(true, mappingSql.Contains("SAPSTORAGELOCATION") && mappingSql.Contains("@WAREHOUSECODE") && mappingSql.Contains("@PLANTCODE") && mappingSql.Contains("@ISACTIVE"), "mapping query filters server side");
            Equal(true, locationSql.Contains("WMS_HU_CURRENTLOCATION") && locationSql.Contains("CURRENT HU") == false && locationSql.Contains("CURRENTHUCOUNT"), "location current HU aggregated without N plus one");
            Equal(true, locationSql.Contains("@WAREHOUSECODE") && locationSql.Contains("@LOCATIONCODE") && locationSql.Contains("@LOCATIONTYPECODE") && locationSql.Contains("@MASTERSTATUS"), "location filters server side");
            Pass();
        }

        private static void AuditOperationQueryIsBoundedAndNormalized()
        {
            FakeAuditRepository repository = new FakeAuditRepository();
            repository.OperationResult.Items.Add(new OperationAuditRowDto { Id = 1, HuCode = "BOX-1", OperationType = "RECEIVE" });
            AuditService service = new AuditService(repository);
            OperationResult<AuditPageResult<OperationAuditRowDto>> result = service.QueryOperations(new OperationAuditQuery
            {
                DateFrom = "2026-08-01", DateTo = "2026-08-07", HuCode = " box-1 ", RequestId = " req-1 ",
                Operator = " user ", OperationType = " receive ", Page = 2, PageSize = 25
            });
            Equal(true, result.IsSuccess, "operation audit query succeeds");
            Equal(new DateTime(2026, 8, 1), repository.LastOperationQuery.FromTime.Value, "operation date inclusive start");
            Equal(new DateTime(2026, 8, 8), repository.LastOperationQuery.ToTimeExclusive.Value, "operation date exclusive end");
            Equal("BOX-1", repository.LastOperationQuery.HuCode, "operation HU normalized");
            Equal("req-1", repository.LastOperationQuery.RequestId, "operation request id preserved");
            Equal("user", repository.LastOperationQuery.Operator, "operation operator normalized");
            Equal("RECEIVE", repository.LastOperationQuery.OperationType, "operation type normalized");
            Equal(2, repository.LastOperationQuery.Page, "operation page retained");
            Equal(25, repository.LastOperationQuery.PageSize, "operation page size retained");
            Equal(false, service.QueryOperations(new OperationAuditQuery { DateFrom = "2026-01-01", DateTo = "2026-04-01" }).IsSuccess, "operation rejects over ninety days");
            Equal(false, service.QueryOperations(new OperationAuditQuery { Page = -1 }).IsSuccess, "operation rejects invalid page");
            Equal(false, service.QueryOperations(new OperationAuditQuery { PageSize = 201 }).IsSuccess, "operation rejects oversized page");
            Equal(true, service.QueryOperations(new OperationAuditQuery { HuCode = "UNKNOWN" }).IsSuccess, "unknown HU is an empty valid query");
            Pass();
        }

        private static void AuditMoveQueryPreservesMovementFacts()
        {
            FakeAuditRepository repository = new FakeAuditRepository();
            repository.MoveResult.Items.Add(new MoveAuditRowDto { Id = 2, HuCode = "BOX-2", HuType = "OUTER_BOX", MoveType = "PUTAWAY", FromLocationCode = "INBOUND-PENDING", ToLocationCode = "H201", RequestId = "REQ-2" });
            AuditService service = new AuditService(repository);
            OperationResult<AuditPageResult<MoveAuditRowDto>> result = service.QueryMoves(new MoveAuditQuery { HuCode = " box-2 ", RequestId = "REQ-2", Operator = "picker", MoveType = " putaway " });
            Equal(true, result.IsSuccess, "move audit query succeeds");
            Equal("BOX-2", repository.LastMoveQuery.HuCode, "move HU normalized");
            Equal("PUTAWAY", repository.LastMoveQuery.MoveType, "move type normalized");
            Equal("INBOUND-PENDING", result.Data.Items.Single().FromLocationCode, "move from location preserved");
            Equal("H201", result.Data.Items.Single().ToLocationCode, "move to location preserved");
            Equal(1, repository.LastMoveQuery.Page, "move default page");
            Equal(50, repository.LastMoveQuery.PageSize, "move default page size");
            Pass();
        }

        private static void AuditHoldQueryCoversActiveAndReleased()
        {
            FakeAuditRepository repository = new FakeAuditRepository();
            repository.HoldResult.Items.Add(new HoldAuditRowDto { Id = 3, HuCode = "BOX-3", IsActive = true, HoldRequestId = "HOLD-3" });
            repository.HoldResult.Items.Add(new HoldAuditRowDto { Id = 4, HuCode = "BOX-4", IsActive = false, HoldRequestId = "HOLD-4", ReleaseRequestId = "REL-4", ReleaseTime = DateTime.Now });
            AuditService service = new AuditService(repository);
            OperationResult<AuditPageResult<HoldAuditRowDto>> all = service.QueryHolds(new HoldAuditQuery { HoldReason = " week ", RequestId = "REL-4" });
            Equal(true, all.IsSuccess, "hold all history query succeeds");
            Equal(null, repository.LastHoldQuery.Status, "hold default includes active and released");
            Equal("week", repository.LastHoldQuery.HoldReason, "hold reason normalized");
            Equal("REL-4", repository.LastHoldQuery.RequestId, "release request uses common request filter");
            Equal(true, service.QueryHolds(new HoldAuditQuery { Status = " active " }).IsSuccess, "active hold filter supported");
            Equal("ACTIVE", repository.LastHoldQuery.Status, "hold active status normalized");
            Equal(true, service.QueryHolds(new HoldAuditQuery { Status = "released" }).IsSuccess, "released hold filter supported");
            Equal(false, service.QueryHolds(new HoldAuditQuery { Status = "UNKNOWN" }).IsSuccess, "unknown hold status rejected");
            Pass();
        }

        private static void AuditRequestTraceMergesPersistedFactsChronologically()
        {
            DateTime start = new DateTime(2026, 8, 20, 10, 0, 0);
            FakeAuditRepository repository = new FakeAuditRepository();
            repository.TraceOperations.Add(new OperationAuditRowDto { Id = 1, OperationType = "PICK", HuCode = "BOX-1", RequestId = "REQ-X", Operator = "A", OperationTime = start.AddMinutes(1) });
            repository.TraceMoves.Add(new MoveAuditRowDto { Id = 2, MoveType = "STAGE", HuCode = "BOX-1", RequestId = "REQ-X", Operator = "A", MoveTime = start, FromLocationCode = "H201", ToLocationCode = "OUTBOUND-STAGING" });
            repository.TraceHolds.Add(new HoldAuditRowDto { Id = 3, HuCode = "BOX-1", HoldReasonCode = "QUALITY", HoldRequestId = "REQ-X", HoldTime = start.AddMinutes(2), HoldUser = "QA" });
            AuditService service = new AuditService(repository);
            OperationResult<RequestAuditTraceDto> result = service.GetRequestTrace(" REQ-X ");
            Equal(true, result.IsSuccess, "request trace succeeds");
            Equal("MOVE,OPERATION,HOLD", string.Join(",", result.Data.Events.Select(x => x.EventType)), "trace merges chronologically");
            Equal("H201 → OUTBOUND-STAGING", result.Data.Events.First().Summary, "move trace preserves from and to");
            Equal(200, repository.LastTraceTop, "request trace bounded per source");
            repository.TraceOperations.Clear(); repository.TraceMoves.Clear(); repository.TraceHolds.Clear();
            Equal(0, service.GetRequestTrace("UNKNOWN").Data.Events.Count, "unknown request returns empty persisted facts");
            Equal(false, service.GetRequestTrace(new string('x', 101)).IsSuccess, "request trace length bounded");
            Pass();
        }

        private static void AuditCapabilitiesDistinguishUnavailableFromZero()
        {
            List<AuditCapabilityDto> capabilities = new AuditService(new FakeAuditRepository()).GetCapabilities();
            Equal(true, capabilities.Single(x => x.Code == "HU_MOVE").Available, "move audit available");
            Equal(true, capabilities.Single(x => x.Code == "HU_OPERATION").Available, "operation audit available");
            Equal(true, capabilities.Single(x => x.Code == "HOLD_RELEASE").Available, "hold audit available");
            Equal(false, capabilities.Single(x => x.Code == "MASTER_DATA").Available, "master data audit unavailable");
            Equal("NOT_OBSERVABLE", capabilities.Single(x => x.Code == "MASTER_DATA").Status, "master data gap is not zero");
            Equal(false, capabilities.Single(x => x.Code == "PRE_RECEIPT_INBOUND_FAILURE").Available, "pre-receipt failure audit unavailable");
            Equal("NOT_OBSERVABLE", capabilities.Single(x => x.Code == "PRE_RECEIPT_INBOUND_FAILURE").Status, "pre-receipt gap is not zero");
            Pass();
        }

        private static void AuditSqlIsPagedParameterizedAndReadOnly()
        {
            string sql = (AuditRepository.OperationRowsSql + AuditRepository.MoveRowsSql + AuditRepository.HoldRowsSql).ToUpperInvariant();
            Equal(true, sql.Contains("WMS_HU_OPERATIONLOG") && sql.Contains("WMS_HU_MOVELOG") && sql.Contains("WMS_HU_HOLDLOG"), "audit reads all persisted sources");
            Equal(true, sql.Contains("INNER JOIN DBO.WMS_HU HU") && sql.Contains("HU.HUCODE") && sql.Contains("HU.HUTYPE"), "audit resolves HU in SQL");
            Equal(true, sql.Contains("@FROMTIME") && sql.Contains("@TOTIMEEXCLUSIVE") && sql.Contains("@HUCODE") && sql.Contains("@REQUESTID") && sql.Contains("@OPERATOR"), "audit common filters parameterized");
            Equal(true, sql.Contains("OFFSET @OFFSET ROWS FETCH NEXT @PAGESIZE ROWS ONLY"), "audit server paging enforced");
            Equal(true, sql.Contains("ORDER BY LOG.OPERATIONTIME DESC, LOG.ID DESC") && sql.Contains("ORDER BY LOG.MOVETIME DESC, LOG.ID DESC") && sql.Contains("ORDER BY LOG.HOLDTIME DESC, LOG.ID DESC"), "audit stable descending order");
            Equal(false, sql.Contains("INSERT ") || sql.Contains("UPDATE ") || sql.Contains("DELETE ") || sql.Contains("MERGE ") || sql.Contains("DROP ") || sql.Contains("ALTER "), "audit SQL is read only");
            Pass();
        }

        private static void MonitoringHealthyOverviewUsesProvableMetrics()
        {
            FakeMonitoringRepository repository = new FakeMonitoringRepository
            {
                Summary = new MonitoringBusinessSummaryDto
                {
                    TodayReceiptCount = 2, TodayBoxCount = 8, TodayPackCount = 24, TodayQty = 960,
                    ActiveHoldBoxCount = 5, InboundExceptionReceiptCount = 7
                }
            };
            FakeWeekCodeAuditService weekCode = new FakeWeekCodeAuditService { TotalCount = 11 };
            MonitoringOverviewDto result = new MonitoringService(repository, new FakeInboundReadService(), weekCode).GetOverview();
            Equal("RUNNING", result.OverallStatus, "healthy monitoring status");
            Equal("CONNECTED", result.WmsDb.Status, "healthy WmsDb status");
            Equal("CONNECTED", result.MesDb.Status, "healthy MesDb status");
            Equal(2, result.TodayReceiptCount.Value, "today successful receipt count");
            Equal(8, result.TodayBoxCount.Value, "today box count");
            Equal(24, result.TodayPackCount.Value, "today pack count");
            Equal(960m, result.TodayQty.Value, "today quantity");
            Equal(5, result.ActiveHoldBoxCount.Value, "active hold count");
            Equal(11, result.WeekCodeAnomalyCount.Value, "weekcode audit count");
            Equal(7, result.InboundExceptionReceiptCount.Value, "inbound exception count");
            Pass();
        }

        private static void MonitoringWmsDbFailureIsDegraded()
        {
            FakeMonitoringRepository repository = new FakeMonitoringRepository { FailWmsProbe = true };
            FakeWeekCodeAuditService weekCode = new FakeWeekCodeAuditService { TotalCount = 3 };
            MonitoringOverviewDto result = new MonitoringService(repository, new FakeInboundReadService(), weekCode).GetOverview();
            Equal("DEGRADED", result.OverallStatus, "WmsDb failure degrades overview");
            Equal("UNAVAILABLE", result.WmsDb.Status, "WmsDb failure status");
            Equal(false, repository.SummaryCalled, "summary skipped when WmsDb unavailable");
            Equal(false, result.TodayReceiptCount.HasValue, "metrics unavailable is not zero");
            Equal(false, weekCode.Called, "weekcode skipped without WmsDb");
            Pass();
        }

        private static void MonitoringMesDbFailureIsDegraded()
        {
            FakeMonitoringRepository repository = new FakeMonitoringRepository { FailMesProbe = true };
            FakeWeekCodeAuditService weekCode = new FakeWeekCodeAuditService { TotalCount = 3 };
            MonitoringOverviewDto result = new MonitoringService(repository, new FakeInboundReadService(), weekCode).GetOverview();
            Equal("DEGRADED", result.OverallStatus, "MesDb failure degrades overview");
            Equal("CONNECTED", result.WmsDb.Status, "WmsDb remains connected");
            Equal("UNAVAILABLE", result.MesDb.Status, "MesDb failure status");
            Equal(true, result.TodayReceiptCount.HasValue, "WmsDb summary remains available");
            Equal(false, result.WeekCodeAnomalyCount.HasValue, "Mes-dependent weekcode marked unavailable");
            Equal(false, weekCode.Called, "weekcode skipped without MesDb");
            Pass();
        }

        private static void MonitoringActivityFillsMissingDaysAndUsesReceipts()
        {
            DateTime dataDate = DateTime.Now.Date.AddDays(-2);
            FakeMonitoringRepository repository = new FakeMonitoringRepository();
            repository.Trend.Add(new SuccessfulReceiptTrendPointDto { Date = dataDate, ReceiptCount = 2, BoxCount = 4, PackCount = 8, Qty = 320 });
            FakeInboundReadService inbound = new FakeInboundReadService();
            inbound.Result.Items.Add(new FinishedGoodsInboundRecordDto { ReceiptId = 9, ReceiveTime = dataDate, BoxCount = 4, InnerPackCount = 8, TotalQty = 320, RequestId = "REQ-9" });
            MonitoringActivityDto result = new MonitoringService(repository, inbound, new FakeWeekCodeAuditService()).GetActivity(7);
            Equal(true, result.TrendAvailable, "trend available");
            Equal(7, result.Trend.Count, "seven day buckets returned");
            Equal(2, result.Trend.First(x => x.Date.Date == dataDate).ReceiptCount, "successful receipt trend retained");
            Equal(0, result.Trend.First().ReceiptCount, "missing trend day returns zero");
            Equal(1, result.RecentReceipts.Count, "recent committed receipt mapped");
            Equal(20, inbound.LastQuery.PageSize, "recent activity bounded to twenty");
            Pass();
        }

        private static void MonitoringContractIsReadOnlyAndSanitized()
        {
            FakeMonitoringRepository repository = new FakeMonitoringRepository { FailMesProbe = true };
            MonitoringOverviewDto overview = new MonitoringService(repository, new FakeInboundReadService(), new FakeWeekCodeAuditService()).GetOverview();
            string json = JObject.FromObject(overview).ToString().ToUpperInvariant();
            Equal(false, overview.FailureHistory.Available, "failure history unavailable capability");
            Equal("NOT_OBSERVABLE", overview.FailureHistory.Status, "unobservable differs from zero failures");
            Equal(false, json.Contains("CONNECTIONSTRING") || json.Contains("PASSWORD") || json.Contains("STACKTRACE") || json.Contains("SERVERNAME"), "monitoring response has no sensitive diagnostics");
            string sql = (MonitoringRepository.SummarySql + MonitoringRepository.TrendSql).ToUpperInvariant();
            Equal(true, sql.Contains("WMS_FINISHEDGOODSINBOUNDRECEIPT") && sql.Contains("GROUP BY CAST(RECEIPT.RECEIVETIME AS DATE)"), "trend derives from successful receipt only");
            Equal(false, sql.Contains("DELETE ") || sql.Contains("UPDATE ") || sql.Contains("INSERT ") || sql.Contains("MERGE "), "monitoring SQL is read only");
            Equal(false, sql.Contains("REQUESTCOUNT") || sql.Contains("SUCCESSRATE"), "monitoring does not claim request total or success rate");
            Pass();
        }

        private static void SapPostingQuanFieldsSerializeAsJsonNumbers()
        {
            SapOutboundPostingPayload payload = PostingPayload();
            JObject json = JObject.Parse(SapOutboundPayloadSerializer.Serialize(payload));
            JObject row = (JObject)json["data"][0];
            Equal(JTokenType.Float, row["LFIMG"].Type, "LFIMG json number");
            Equal(JTokenType.Float, row["LFIMG_BOX"].Type, "LFIMG_BOX json number");
            Equal(true, row["NTGEW"].Type == JTokenType.Integer || row["NTGEW"].Type == JTokenType.Float, "NTGEW json number");
            Equal(true, row["BRGEW"].Type == JTokenType.Integer || row["BRGEW"].Type == JTokenType.Float, "BRGEW json number");
            Pass();
        }

        private static void SapPostingCharFieldsRemainStrings()
        {
            JObject row = (JObject)JObject.Parse(SapOutboundPayloadSerializer.Serialize(PostingPayload()))["data"][0];
            Equal(JTokenType.String, row["VBELN"].Type, "VBELN json string");
            Equal(JTokenType.String, row["POSNR"].Type, "POSNR json string");
            Equal("000010", row.Value<string>("POSNR"), "POSNR leading zeros");
            Pass();
        }

        private static void SapPostingLfimgBoxUsesCurrentLineQuantity()
        {
            SapOutboundPostingRow single = new SapOutboundPostingRow { BOXNO="B-SINGLE",POSNR="000010",CHARG="C1",LFIMG=70m,LFIMG_BOX=70m };
            List<SapOutboundPostingRow> mixed = new List<SapOutboundPostingRow>
            {
                new SapOutboundPostingRow { BOXNO="B-MIXED",POSNR="000010",CHARG="A000472425",LFIMG=40m,LFIMG_BOX=40m },
                new SapOutboundPostingRow { BOXNO="B-MIXED",POSNR="000010",CHARG="A000572425",LFIMG=30m,LFIMG_BOX=30m }
            };
            List<SapOutboundPostingRow> crossLine = new List<SapOutboundPostingRow>
            {
                new SapOutboundPostingRow { BOXNO="B-CROSS",POSNR="000010",CHARG="C1",LFIMG=20m,LFIMG_BOX=20m },
                new SapOutboundPostingRow { BOXNO="B-CROSS",POSNR="000020",CHARG="C1",LFIMG=15m,LFIMG_BOX=15m },
                new SapOutboundPostingRow { BOXNO="B-CROSS",POSNR="000020",CHARG="C2",LFIMG=35m,LFIMG_BOX=35m }
            };
            Equal(70m, single.LFIMG_BOX, "single batch LFIMG_BOX");
            Equal(true, mixed.All(x => x.LFIMG_BOX == x.LFIMG), "mixed CHARG line quantities");
            Equal(70m, mixed.Sum(x => x.LFIMG), "mixed CHARG box invariant");
            Equal(false, mixed.All(x => x.LFIMG_BOX == 70m), "whole-box quantity is not repeated");
            Equal(true, crossLine.All(x => x.LFIMG_BOX == x.LFIMG), "cross POSNR and CHARG line quantities");
            Equal(70m, crossLine.Sum(x => x.LFIMG), "cross POSNR and CHARG box invariant");
            string repository = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "SapOutboundPostingRepository.cs"));
            Equal(true, repository.Contains("LFIMG = lineQuantity") && repository.Contains("LFIMG_BOX = lineQuantity"), "repository assigns both quantities from current group");
            Equal(false, repository.Contains("LFIMG_BOX = first.EffectiveQty"), "repository does not repeat whole-box quantity");
            Pass();
        }

        private static void SapPostingS0089ConfigurationIsEndpointOnly()
        {
            string client = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS", "Infrastructure", "Sap", "S0089SapOutboundPostingClient.cs"));
            string config = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS", "Web.config"));
            Equal(true, client.Contains("Sap.S0089.Endpoint") && client.Contains("DefaultTimeoutMs"), "endpoint required and timeout optional default");
            Equal(false, client.Contains("Username") || client.Contains("Password") || client.Contains("NetworkCredential") || client.Contains("PreAuthenticate") || client.Contains("Authorization"), "client has no authentication convention");
            Equal(false, config.Contains("Sap.S0089.Username") || config.Contains("Sap.S0089.Password"), "host config has no credential keys");
            Pass();
        }

        private static void SapPostingConfigurationFailurePreventsClaim()
        {
            FakeSapPostingRepository repository = new FakeSapPostingRepository();
            FakeSapPostingClient client = new FakeSapPostingClient { ConfigurationValid = false };
            OperationResult<OutboundShippingStateDto> result = new SapOutboundPostingService(repository, client).ConfirmShipment("8001", Control("REQ-CFG"));
            Equal(false, result.IsSuccess, "invalid config fails locally");
            Equal(0, repository.PreviewCalls, "invalid config skips snapshot");
            Equal(0, repository.ClaimCalls, "invalid config skips claim");
            Equal(0, client.PostCalls, "invalid config skips invocation");
            Pass();
        }

        private static void SapPostingSendsAllRowsInOneCall()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository();
            repository.Preview.Data.Add(new SapOutboundPostingRow { VBELN="8001",POSNR="000020",LGORT="31C3",BOXNO="B2",MATNR="M2",CHARG="C2",LFIMG=2,LFIMG_BOX=2,GEWEI="KG",VRKME="",BOXSIZE="" });
            repository.Preview.PayloadJson = SapOutboundPayloadSerializer.Serialize(new SapOutboundPostingPayload { Data = repository.Preview.Data });
            FakeSapPostingClient client = new FakeSapPostingClient();
            OperationResult<OutboundShippingStateDto> result = new SapOutboundPostingService(repository, client).ConfirmShipment("8001", Control("REQ-ONE"));
            Equal(true, result.IsSuccess, "multi row confirm succeeds");
            Equal(1, client.PostCalls, "one SAP invocation");
            Equal(2, JObject.Parse(client.LastPayload)["data"].Count(), "all POSNR in one request");
            Pass();
        }

        private static void SapPostingRequiresSapBatchWithoutFallback()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "SapOutboundPostingRepository.cs"));
            Equal(true, source.Contains("pdi.SAPBatchNo"), "snapshot reads SAPBatchNo");
            Equal(true, source.Contains("SAP_POSTING_CHARG_MISSING"), "missing SAPBatchNo blocked");
            Equal(false, source.Contains("pdi.BatchNo,pdi.AllocatedQty"), "no internal batch fallback projection");
            Pass();
        }

        private static void SapPostingUsesLineStorageLocation()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "SapOutboundPostingRepository.cs"));
            Equal(true, source.Contains("l.SAPStorageLocation"), "LGORT comes from order line");
            Equal(false, source.Contains("LocationCode AS LGORT"), "physical location not LGORT");
            Pass();
        }

        private static void SapPostingV1DefaultsAreStable()
        {
            SapOutboundPostingRow row = PostingPayload().Data.Single();
            Equal("", row.BOXSIZE, "BOXSIZE empty"); Equal("", row.VRKME, "VRKME empty");
            Equal(0m, row.NTGEW, "NTGEW zero"); Equal(0m, row.BRGEW, "BRGEW zero"); Equal("KG", row.GEWEI, "GEWEI KG");
            Pass();
        }

        private static void SapPostingSuccessShipsWithSapSource()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository();
            FakeSapPostingClient client = new FakeSapPostingClient { TransportStatus = "SUCCESS", SapCode = "S" };
            OutboundShippingStateDto state = new SapOutboundPostingService(repository, client).ConfirmShipment("8001", Control("REQ-S")).Data;
            Equal("SHIPPED", state.OutboundStatus, "SAP S ships order"); Equal("SUCCESS", state.SapPostingStatus, "SAP S posting status");
            Equal("SAP_RESPONSE", state.SuccessConfirmSource, "SAP response source");
            Pass();
        }

        private static void SapPostingExplicitFailureKeepsPicked()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository();
            FakeSapPostingClient client = new FakeSapPostingClient { TransportStatus = "FAILED", SapCode = "E" };
            OutboundShippingStateDto state = new SapOutboundPostingService(repository, client).ConfirmShipment("8001", Control("REQ-E")).Data;
            Equal("PICKED", state.OutboundStatus, "SAP E keeps picked"); Equal("FAILED", state.SapPostingStatus, "SAP E failed"); Equal(false, state.CanRetry, "explicit E no retry before IT confirmation");
            Pass();
        }

        private static void SapPostingUnknownRequiresItReview()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository();
            FakeSapPostingClient client = new FakeSapPostingClient { TransportStatus = "UNKNOWN" };
            OutboundShippingStateDto state = new SapOutboundPostingService(repository, client).ConfirmShipment("8001", Control("REQ-U")).Data;
            Equal("PICKED", state.OutboundStatus, "unknown keeps picked"); Equal(true, state.RequiresITReview, "unknown IT review"); Equal(false, state.CanRetry, "unknown no retry");
            Pass();
        }

        private static void SapPostingRetryUsesFrozenPayload()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository();
            repository.RetryPayload = "{\"data\":[{\"VBELN\":\"8001\"}]}";
            FakeSapPostingClient client = new FakeSapPostingClient();
            new SapOutboundPostingService(repository, client).Retry("8001", Control("REQ-R2"));
            Equal(repository.RetryPayload, client.LastPayload, "retry uses frozen payload"); Equal(0, repository.PreviewCalls, "retry does not rebuild preview");
            Pass();
        }

        private static void SapPostingManualPostedUsesNoSapCall()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository(); FakeSapPostingClient client = new FakeSapPostingClient();
            OutboundShippingStateDto state = new SapOutboundPostingService(repository, client).ManualConfirmPosted("8001", Manual("REQ-MP")).Data;
            Equal(0, client.PostCalls, "manual posted no SAP call"); Equal("MANUAL_CONFIRM", state.SuccessConfirmSource, "manual source"); Equal("SHIPPED", state.OutboundStatus, "manual posted ships");
            Pass();
        }

        private static void SapPostingManualNotPostedBecomesRetryable()
        {
            FakeSapPostingRepository repository = ReadyPostingRepository(); FakeSapPostingClient client = new FakeSapPostingClient();
            OutboundShippingStateDto state = new SapOutboundPostingService(repository, client).ManualConfirmNotPosted("8001", Manual("REQ-MN")).Data;
            Equal(0, client.PostCalls, "manual not posted no SAP call"); Equal("FAILED", state.SapPostingStatus, "manual not posted failed"); Equal(true, state.CanRetry, "manual not posted retry eligible");
            Pass();
        }

        private static void SapPostingLegacyShipAliasClosesBypass()
        {
            string module = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS", "Modules", "OutboundModule.cs"));
            int route = module.IndexOf("Post(\"/orders/{vbeln}/ship\"", StringComparison.Ordinal);
            string block = module.Substring(route, Math.Min(1500, module.Length - route));
            Equal(true, block.Contains("_sapPostingService.ConfirmShipment"), "legacy ship delegates posting service");
            Equal(false, block.Contains("_formalService.ShipOrder"), "legacy direct ship removed");
            Pass();
        }

        private static void SapDeliveryReceiveAddsGenericAudit()
        {
            string module = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS", "Modules", "SapOutboundIntegrationModule.cs"));
            Equal(true, module.Contains("RecordReceiveCommunication"), "receive generic audit");
            Equal(true, module.Contains("WMS_OutboundOrderReceive" ) == false, "receive module preserves repository boundary");
            Pass();
        }

        private static void SapPostingMigrationAndRepositoryContractsAreSafe()
        {
            string migration = File.ReadAllText(Path.Combine(ProjectRoot(), "docs", "database", "018_OutboundSapPosting.sql")).ToUpperInvariant();
            string repository = File.ReadAllText(Path.Combine(ProjectRoot(), "WMS-HU-Model", "Repositories", "Impl", "SapOutboundPostingRepository.cs")).ToUpperInvariant();
            Equal(true, migration.Contains("SET XACT_ABORT ON") && migration.Contains("UNIQUE INDEX UX_WMS_OUTBOUNDSAPPOSTING_DELIVERY") && migration.Contains("SAPBATCHNO IS MISSING"), "migration gates and uniqueness");
            Equal(true, repository.Contains("ISOLATIONLEVEL.SERIALIZABLE") && repository.Contains("UPDLOCK,HOLDLOCK,ROWLOCK"), "serializable claim locking");
            Equal(false, repository.Contains("ADDWITHVALUE") || repository.Contains(" DYNAMIC "), "typed parameters and no dynamic");
            Pass();
        }

        private static SapOutboundPostingPayload PostingPayload()
        {
            return new SapOutboundPostingPayload { Data = new List<SapOutboundPostingRow> { new SapOutboundPostingRow { VBELN="8001",POSNR="000010",LGORT="31C3",BOXNO="B1",MATNR="M1",CHARG="C1",LFIMG=28.000000m,LFIMG_BOX=28.000000m,NTGEW=0m,BRGEW=0m,GEWEI="KG",VRKME="",BOXSIZE="" } } };
        }

        private static FakeSapPostingRepository ReadyPostingRepository()
        {
            FakeSapPostingRepository repository = new FakeSapPostingRepository();
            repository.Preview = new SapOutboundPostingPreviewDto { SAPDeliveryNo="8001",Data=PostingPayload().Data };
            repository.Preview.PayloadJson = SapOutboundPayloadSerializer.Serialize(new SapOutboundPostingPayload { Data=repository.Preview.Data });
            repository.Preview.PayloadHash = SapOutboundPayloadSerializer.ComputeHash(repository.Preview.PayloadJson);
            return repository;
        }

        private static SapOutboundPostingControlRequest Control(string id) { return new SapOutboundPostingControlRequest { RequestId=id,Operator="tester",Remark="test" }; }
        private static SapOutboundManualConfirmRequest Manual(string id) { return new SapOutboundManualConfirmRequest { RequestId=id,Operator="it",Remark="checked in SAP" }; }
        private static string ProjectRoot() { return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")); }

        private static UpdateLocationRequest LocationUpdate(string operatorName)
        {
            return new UpdateLocationRequest { ZoneCode = "STORAGE", LocationTypeCode = "DOUBLE_RACK", ManageMode = "CAPACITY_LOCATION", CapacityMode = "STACK_POSITION", PlantPolicy = "SAME_PLANT", LocationLevel = "BASE", MasterStatus = "ACTIVE", Operator = operatorName };
        }

        private class FakeSapPostingClient : ISapOutboundPostingClient
        {
            public bool ConfigurationValid = true;
            public int PostCalls;
            public string LastPayload;
            public string TransportStatus = "SUCCESS";
            public string SapCode = "S";
            public SapOutboundTransportConfigurationResult ValidateConfiguration()
            {
                return ConfigurationValid
                    ? new SapOutboundTransportConfigurationResult { IsValid=true,Configuration=new SapOutboundTransportConfiguration { Endpoint="http://example.invalid",TimeoutMs=1000 } }
                    : new SapOutboundTransportConfigurationResult { IsValid=false,Message="missing" };
            }
            public SapOutboundTransportResult Post(string payloadJson, SapOutboundTransportConfiguration configuration)
            {
                PostCalls++; LastPayload=payloadJson;
                return new SapOutboundTransportResult { InvocationStarted=true,ResultStatus=TransportStatus,SapCode=SapCode,SapMessage=TransportStatus,ResponseJson="{}",RequestTime=DateTime.Now,ResponseTime=DateTime.Now };
            }
        }

        private class FakeSapPostingRepository : ISapOutboundPostingRepository
        {
            public int PreviewCalls;
            public int ClaimCalls;
            public SapOutboundPostingPreviewDto Preview = new SapOutboundPostingPreviewDto();
            public string RetryPayload = "{\"data\":[]}";
            public SapPostingSnapshotBuildResult BuildPreview(string sapDeliveryNo) { PreviewCalls++; return new SapPostingSnapshotBuildResult { IsSuccess=true,Preview=Preview }; }
            public SapPostingClaimResult ClaimInitial(string sapDeliveryNo,SapOutboundPostingControlRequest request,SapOutboundPostingPreviewDto preview) { ClaimCalls++; return new SapPostingClaimResult { IsSuccess=true,ShouldInvokeSap=true,PostingId=1,AttemptNo=1,PayloadJson=preview.PayloadJson,PayloadHash=preview.PayloadHash }; }
            public SapPostingClaimResult ClaimRetry(string sapDeliveryNo,SapOutboundPostingControlRequest request) { ClaimCalls++; return new SapPostingClaimResult { IsSuccess=true,ShouldInvokeSap=true,PostingId=1,AttemptNo=2,PayloadJson=RetryPayload,PayloadHash="HASH" }; }
            public OutboundShippingStateDto FinalizeAttempt(long postingId,int attemptNo,string requestId,SapOutboundTransportResult transport,string operatorValue,string remark)
            {
                if (transport.ResultStatus=="SUCCESS") return new OutboundShippingStateDto { SAPDeliveryNo="8001",OutboundStatus="SHIPPED",SapPostingStatus="SUCCESS",SuccessConfirmSource="SAP_RESPONSE" };
                if (transport.ResultStatus=="FAILED") return new OutboundShippingStateDto { SAPDeliveryNo="8001",OutboundStatus="PICKED",SapPostingStatus="FAILED",CanRetry=false };
                return new OutboundShippingStateDto { SAPDeliveryNo="8001",OutboundStatus="PICKED",SapPostingStatus="UNKNOWN",RequiresITReview=true };
            }
            public OutboundShippingStateDto GetShippingState(string sapDeliveryNo) { return new OutboundShippingStateDto { SAPDeliveryNo=sapDeliveryNo,OutboundStatus="PICKED",SapPostingStatus="READY",CanConfirmShipment=true }; }
            public OutboundShippingStateDto ManualConfirmPosted(string sapDeliveryNo,SapOutboundManualConfirmRequest request) { return new OutboundShippingStateDto { SAPDeliveryNo=sapDeliveryNo,OutboundStatus="SHIPPED",SapPostingStatus="SUCCESS",SuccessConfirmSource="MANUAL_CONFIRM" }; }
            public OutboundShippingStateDto ManualConfirmNotPosted(string sapDeliveryNo,SapOutboundManualConfirmRequest request) { return new OutboundShippingStateDto { SAPDeliveryNo=sapDeliveryNo,OutboundStatus="PICKED",SapPostingStatus="FAILED",CanRetry=true }; }
            public PagedResult<OutboundShippingStateDto> QueryShippingOrders(string sapDeliveryNo,string outboundStatus,string postingStatus,int page,int pageSize) { return new PagedResult<OutboundShippingStateDto>(); }
            public PagedResult<SapIntegrationLogDto> QueryLogs(SapIntegrationLogQuery query) { return new PagedResult<SapIntegrationLogDto>(); }
            public SapIntegrationLogDto GetLog(long id) { return null; }
            public void RecordReceiveCommunication(string businessKey,string requestJson,string resultStatus,string externalMessage,string payloadHash) { }
        }

        private class FakeMasterDataMaintenanceRepository : IMasterDataMaintenanceRepository
        {
            public Dictionary<string, WarehouseDto> Warehouses = new Dictionary<string, WarehouseDto>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, WarehousePlantDto> Mappings = new Dictionary<string, WarehousePlantDto>(StringComparer.OrdinalIgnoreCase);
            public string LastCreatedWarehouseCode; public string LastUpdatedWarehouseCode;
            public bool HasActiveLocationValue; public bool HasCurrentHuValue; public bool HasActiveMappingValue;
            public List<WarehouseDto> QueryWarehouses(WarehouseQuery query) { return Warehouses.Values.ToList(); }
            public WarehouseDto GetWarehouse(string warehouseCode) { WarehouseDto value; return Warehouses.TryGetValue(warehouseCode, out value) ? value : null; }
            public bool WarehouseExists(string warehouseCode) { return Warehouses.ContainsKey(warehouseCode); }
            public long InsertWarehouse(WarehouseCreateRequest request) { LastCreatedWarehouseCode = request.WarehouseCode; Warehouses[request.WarehouseCode] = new WarehouseDto { Id = Warehouses.Count + 1, WarehouseCode = request.WarehouseCode, WarehouseName = request.WarehouseName, WarehouseType = request.WarehouseType, IsActive = true }; return Warehouses.Count; }
            public bool UpdateWarehouse(string warehouseCode, WarehouseUpdateRequest request) { LastUpdatedWarehouseCode = warehouseCode; Warehouses[warehouseCode].WarehouseName = request.WarehouseName; return true; }
            public bool SetWarehouseActive(string warehouseCode, bool isActive, string operatorName) { Warehouses[warehouseCode].IsActive = isActive; return true; }
            public bool HasActiveLocations(string warehouseCode) { return HasActiveLocationValue; }
            public bool HasCurrentHus(string warehouseCode) { return HasCurrentHuValue; }
            public bool HasActiveMappings(string warehouseCode) { return HasActiveMappingValue; }
            public List<WarehousePlantDto> QueryWarehousePlants(WarehousePlantQuery query) { return Mappings.Values.ToList(); }
            public WarehousePlantDto GetWarehousePlant(string warehouseCode, string plantCode) { WarehousePlantDto value; return Mappings.TryGetValue(warehouseCode + "|" + plantCode, out value) ? value : null; }
            public bool WarehousePlantExists(string warehouseCode, string plantCode) { return Mappings.ContainsKey(warehouseCode + "|" + plantCode); }
            public long InsertWarehousePlant(WarehousePlantCreateRequest request) { Mappings[request.WarehouseCode + "|" + request.PlantCode] = new WarehousePlantDto { WarehouseCode = request.WarehouseCode, PlantCode = request.PlantCode, SapStorageLocation = request.SapStorageLocation, IsActive = true }; return Mappings.Count; }
            public bool UpdateWarehousePlant(string warehouseCode, string plantCode, WarehousePlantUpdateRequest request) { Mappings[warehouseCode + "|" + plantCode].SapStorageLocation = request.SapStorageLocation; return true; }
            public bool SetWarehousePlantActive(string warehouseCode, string plantCode, bool isActive) { Mappings[warehouseCode + "|" + plantCode].IsActive = isActive; return true; }
            public List<MasterDataOptionDto> GetWarehouseOptions() { return new List<MasterDataOptionDto>(); }
            public List<MasterDataOptionDto> GetZoneOptions(string warehouseCode) { return new List<MasterDataOptionDto>(); }
        }

        private class FakeLocationMaintenanceRepository : ILocationRepository
        {
            public Dictionary<string, LocationDto> Locations = new Dictionary<string, LocationDto>(StringComparer.OrdinalIgnoreCase);
            public bool WarehouseActive; public int CurrentHuCount; public bool OccupyOnStatusUpdate;
            public List<LocationDto> GetList(string warehouseCode, string zoneCode) { return Locations.Values.ToList(); }
            public List<LocationDto> GetMaintenanceList(LocationMaintenanceQuery query) { return Locations.Values.ToList(); }
            public LocationDto GetByCode(string warehouseCode, string locationCode) { LocationDto value; return Locations.TryGetValue(locationCode, out value) ? value : null; }
            public long? GetWarehouseId(string warehouseCode) { return WarehouseActive ? (long?)1 : null; }
            public long? GetWarehouseIdAny(string warehouseCode) { return 1; }
            public long? GetZoneId(long warehouseId, string zoneCode) { return 1; }
            public long? GetLocationId(long warehouseId, string locationCode) { LocationDto value; return Locations.TryGetValue(locationCode, out value) ? value.Id : (long?)null; }
            public bool Exists(long warehouseId, string locationCode) { return Locations.ContainsKey(locationCode); }
            public long Insert(long warehouseId, long zoneId, long? parentLocationId, CreateLocationRequest request) { Locations[request.LocationCode] = new LocationDto { Id = Locations.Count + 1, WarehouseCode = request.WarehouseCode, ZoneCode = request.ZoneCode, LocationCode = request.LocationCode, LocationTypeCode = request.LocationTypeCode, MasterStatus = "ACTIVE" }; return Locations.Count; }
            public bool Update(long locationId, long zoneId, long? parentLocationId, UpdateLocationRequest request) { return true; }
            public int CountCurrentHus(long locationId) { return CurrentHuCount; }
            public bool SetMasterStatus(long locationId, string masterStatus, string operatorName) { if (masterStatus == "INACTIVE" && (CurrentHuCount > 0 || OccupyOnStatusUpdate)) return false; Locations.Values.First(x => x.Id == locationId).MasterStatus = masterStatus; return true; }
        }

        private class FakeAuditRepository : IAuditRepository
        {
            public OperationAuditQuery LastOperationQuery;
            public MoveAuditQuery LastMoveQuery;
            public HoldAuditQuery LastHoldQuery;
            public int LastTraceTop;
            public AuditPageResult<OperationAuditRowDto> OperationResult = new AuditPageResult<OperationAuditRowDto>();
            public AuditPageResult<MoveAuditRowDto> MoveResult = new AuditPageResult<MoveAuditRowDto>();
            public AuditPageResult<HoldAuditRowDto> HoldResult = new AuditPageResult<HoldAuditRowDto>();
            public List<OperationAuditRowDto> TraceOperations = new List<OperationAuditRowDto>();
            public List<MoveAuditRowDto> TraceMoves = new List<MoveAuditRowDto>();
            public List<HoldAuditRowDto> TraceHolds = new List<HoldAuditRowDto>();
            public AuditPageResult<OperationAuditRowDto> QueryOperations(OperationAuditQuery query) { LastOperationQuery = query; OperationResult.Page = query.Page; OperationResult.PageSize = query.PageSize; return OperationResult; }
            public AuditPageResult<MoveAuditRowDto> QueryMoves(MoveAuditQuery query) { LastMoveQuery = query; MoveResult.Page = query.Page; MoveResult.PageSize = query.PageSize; return MoveResult; }
            public AuditPageResult<HoldAuditRowDto> QueryHolds(HoldAuditQuery query) { LastHoldQuery = query; HoldResult.Page = query.Page; HoldResult.PageSize = query.PageSize; return HoldResult; }
            public List<OperationAuditRowDto> GetOperationsByRequestId(string requestId, int top) { LastTraceTop = top; return TraceOperations; }
            public List<MoveAuditRowDto> GetMovesByRequestId(string requestId, int top) { LastTraceTop = top; return TraceMoves; }
            public List<HoldAuditRowDto> GetHoldsByRequestId(string requestId, int top) { LastTraceTop = top; return TraceHolds; }
        }

        private class FakeMonitoringRepository : IMonitoringRepository
        {
            public bool FailWmsProbe; public bool FailMesProbe; public bool SummaryCalled;
            public MonitoringBusinessSummaryDto Summary = new MonitoringBusinessSummaryDto();
            public List<SuccessfulReceiptTrendPointDto> Trend = new List<SuccessfulReceiptTrendPointDto>();
            public void ProbeWmsDb() { if (FailWmsProbe) throw new InvalidOperationException("sensitive WmsDb failure"); }
            public void ProbeMesDb() { if (FailMesProbe) throw new InvalidOperationException("sensitive MesDb failure"); }
            public MonitoringBusinessSummaryDto GetBusinessSummary(DateTime fromTime, DateTime toTimeExclusive) { SummaryCalled = true; return Summary; }
            public List<SuccessfulReceiptTrendPointDto> GetSuccessfulReceiptTrend(DateTime fromTime, DateTime toTimeExclusive) { return Trend; }
        }

        private class FakeInboundReadService : IFinishedGoodsInboundReadService
        {
            public FinishedGoodsInboundRecordQuery LastQuery;
            public FinishedGoodsInboundRecordResultDto Result = new FinishedGoodsInboundRecordResultDto { Items = new List<FinishedGoodsInboundRecordDto>() };
            public OperationResult<FinishedGoodsInboundRecordResultDto> Query(FinishedGoodsInboundRecordQuery query) { LastQuery = query; return OperationResult<FinishedGoodsInboundRecordResultDto>.Success(Result); }
            public OperationResult<FinishedGoodsInboundDetailDto> GetDetail(long receiptId) { throw new NotSupportedException(); }
        }

        private class FakeWeekCodeAuditService : IWeekCodeAuditService
        {
            public int TotalCount; public bool Called;
            public OperationResult<WeekCodeAuditResultDto> Query(WeekCodeAuditQuery query) { Called = true; return OperationResult<WeekCodeAuditResultDto>.Success(new WeekCodeAuditResultDto { TotalCount = TotalCount }); }
        }

        private static FakeHuRepository HuHierarchy()
        {
            FakeHuRepository repository = new FakeHuRepository();
            repository.Hus["PALLET-1"] = new HuDto { Id = 1, HuCode = "PALLET-1", HuType = "PALLET", LifecycleStatus = "ACTIVE" };
            repository.Hus["BOX-1"] = new HuDto { Id = 2, HuCode = "BOX-1", HuType = "OUTER_BOX", LifecycleStatus = "ACTIVE" };
            repository.Hus["PACK-1"] = new HuDto { Id = 3, HuCode = "PACK-1", HuType = "INNER_PACK", LifecycleStatus = "ACTIVE" };
            repository.ParentIds[2] = 1;
            repository.ParentIds[3] = 2;
            repository.Tree = new List<HuTreeFlatRow>
            {
                new HuTreeFlatRow { HuId = 1, HuCode = "PALLET-1", HuType = "PALLET", LevelNo = 0 },
                new HuTreeFlatRow { HuId = 2, ParentHuId = 1, HuCode = "BOX-1", HuType = "OUTER_BOX", LevelNo = 1 },
                new HuTreeFlatRow { HuId = 3, ParentHuId = 2, HuCode = "PACK-1", HuType = "INNER_PACK", LevelNo = 2 }
            };
            return repository;
        }

        private class FakeHuRepository : IHuRepository
        {
            public Dictionary<string, HuDto> Hus = new Dictionary<string, HuDto>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<long, long> ParentIds = new Dictionary<long, long>();
            public List<HuTreeFlatRow> Tree = new List<HuTreeFlatRow>();
            public HuDto GetByCode(string huCode) { HuDto hu; return Hus.TryGetValue(huCode, out hu) ? hu : null; }
            public long? GetHuId(string huCode) { HuDto hu = GetByCode(huCode); return hu == null ? (long?)null : hu.Id; }
            public string GetHuType(long huId) { return Hus.Values.First(x => x.Id == huId).HuType; }
            public string GetPlantCode(long huId) { return Hus.Values.First(x => x.Id == huId).PlantCode; }
            public long? GetPackageSpecId(string packageSpecCode) { return null; }
            public HuDto GetActiveParent(long childHuId) { long parentId; return ParentIds.TryGetValue(childHuId, out parentId) ? Hus.Values.First(x => x.Id == parentId) : null; }
            public bool HasActiveParent(long childHuId) { return ParentIds.ContainsKey(childHuId); }
            public bool WouldCreateCycle(long parentHuId, long childHuId) { return false; }
            public long Create(CreateHuRequest request, long? packageSpecId, string operationNo) { throw new NotSupportedException(); }
            public void Attach(long parentHuId, long childHuId, AttachHuRequest request, string operationNo) { throw new NotSupportedException(); }
            public long AddItem(long huId, AddHuItemRequest request, WeekCodeSnapshot weekCode, string operationNo) { throw new NotSupportedException(); }
            public List<HuTreeFlatRow> GetTree(long rootHuId) { return Tree; }
            public List<HuItemDto> GetItems(long huId) { return new List<HuItemDto>(); }
        }

        private class FakeHuLocationService : IHuLocationService
        {
            public Dictionary<string, HuLocationDto> Locations = new Dictionary<string, HuLocationDto>(StringComparer.OrdinalIgnoreCase);
            public OperationResult<HuLocationDto> GetLocation(string huCode)
            {
                HuLocationDto location;
                if (!Locations.TryGetValue(huCode, out location)) location = new HuLocationDto { HuCode = huCode };
                return OperationResult<HuLocationDto>.Success(location);
            }
            public OperationResult<HuMoveResultDto> Move(string huCode, HuMoveRequest request) { throw new NotSupportedException(); }
            public OperationResult<List<HuMoveLogDto>> GetMoveLogs(string huCode, int top) { throw new NotSupportedException(); }
        }

        private class FakeHuDetailRepository : IHuDetailRepository
        {
            public List<HuDetailContentDto> Contents = new List<HuDetailContentDto>();
            public HuHoldLogDto ActiveHold;
            public string LastContentHuType;
            public int LastOperationTop;
            public int LastHoldTop;
            public List<HuHoldLogDto> HoldLogs = new List<HuHoldLogDto>();
            public List<HuDetailContentDto> GetContents(long huId, string huType) { LastContentHuType = huType; return Contents; }
            public HuHoldLogDto GetActiveHold(long huId) { return ActiveHold; }
            public List<HuOperationLogDto> GetOperationLogs(long huId, int top) { LastOperationTop = top; return new List<HuOperationLogDto>(); }
            public List<HuHoldLogDto> GetHoldLogs(long huId, int top) { LastHoldTop = top; return HoldLogs; }
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
