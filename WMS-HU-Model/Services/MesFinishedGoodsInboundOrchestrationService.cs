using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class MesFinishedGoodsInboundOrchestrationService : IMesFinishedGoodsInboundOrchestrationService
    {
        private readonly IMesFinishedGoodsInboundRepository _mesRepository;
        private readonly MesFinishedGoodsInboundSnapshotBuilder _snapshotBuilder;
        private readonly MesWeekCodeRuleProvider _ruleProvider;
        private readonly IFinishedGoodsInboundService _inboundService;

        public MesFinishedGoodsInboundOrchestrationService(
            IMesFinishedGoodsInboundRepository mesRepository,
            MesFinishedGoodsInboundSnapshotBuilder snapshotBuilder,
            MesWeekCodeRuleProvider ruleProvider,
            IFinishedGoodsInboundService inboundService)
        {
            _mesRepository = mesRepository ?? throw new ArgumentNullException(nameof(mesRepository));
            _snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
            _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
            _inboundService = inboundService ?? throw new ArgumentNullException(nameof(inboundService));
        }

        public OperationResult<MesFinishedGoodsInboundTriggerResult> Trigger(string planStockId)
        {
            if (string.IsNullOrWhiteSpace(planStockId)) return OperationResult<MesFinishedGoodsInboundTriggerResult>.Fail("planStockId不能为空。");
            MesFinishedGoodsInboundSourceGraph source = _mesRepository.GetPlanStock(planStockId.Trim());
            OperationResult<FinishedGoodsInboundReceiveRequest> built = _snapshotBuilder.Build(source);
            if (!built.IsSuccess) return OperationResult<MesFinishedGoodsInboundTriggerResult>.Fail(built.Message);
            EnrichWeekRules(built.Data);
            OperationResult<FinishedGoodsInboundResultDto> received = _inboundService.Receive(built.Data);
            if (!received.IsSuccess) return OperationResult<MesFinishedGoodsInboundTriggerResult>.Fail(received.Message);
            return OperationResult<MesFinishedGoodsInboundTriggerResult>.Success(new MesFinishedGoodsInboundTriggerResult
            {
                PlanStockId = built.Data.Snapshot.SourceEntityId,
                ReceiptId = received.Data.ReceiptId,
                IsDuplicate = received.Data.IsDuplicate
            });
        }

        private void EnrichWeekRules(FinishedGoodsInboundReceiveRequest request)
        {
            List<BoxRuleLookup> lookups = new List<BoxRuleLookup>();
            foreach (FinishedGoodsInboundBoxSnapshot box in request.Snapshot.Boxes)
            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                lookups.Add(new BoxRuleLookup
                {
                    BoxSourceEntityId = box.SourceEntityId,
                    Rule = _ruleProvider.Resolve(request.Snapshot.PlantCode, box.MaterialCode, box.MaterialRevision)
                });
            foreach (FinishedGoodsInboundBoxSnapshot box in request.Snapshot.Boxes)
            {
                List<MesWeekCodeRuleLookupResult> boxRules = lookups
                    .Where(x => string.Equals(x.BoxSourceEntityId, box.SourceEntityId, StringComparison.Ordinal))
                    .Select(x => x.Rule)
                    .ToList();
                if (boxRules.Count == 0 || boxRules.Any(x => !x.IsSuccess)) continue;
                List<string> distinctPairs = boxRules.Select(x => Pair(x.RuleSource, x.RuleValue)).Distinct(StringComparer.Ordinal).ToList();
                if (distinctPairs.Count != 1) continue;
                box.WeekRuleSource = boxRules[0].RuleSource;
                box.WeekRuleValue = boxRules[0].RuleValue;
            }
        }

        private static string Pair(string source, string value)
        {
            return (source ?? string.Empty).Length + ":" + (source ?? string.Empty) + (value ?? string.Empty).Length + ":" + (value ?? string.Empty);
        }

        private class BoxRuleLookup
        {
            public string BoxSourceEntityId { get; set; }
            public MesWeekCodeRuleLookupResult Rule { get; set; }
        }
    }
}
