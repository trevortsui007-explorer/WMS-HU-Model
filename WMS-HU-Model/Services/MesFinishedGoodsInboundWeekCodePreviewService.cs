using System;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Services
{
    public class MesFinishedGoodsInboundWeekCodePreviewService
    {
        private readonly MesFinishedGoodsInboundSnapshotBuilder _builder;
        private readonly MesWeekCodeRuleProvider _provider;
        private readonly IWeekCodeResolver _resolver;
        public MesFinishedGoodsInboundWeekCodePreviewService(MesFinishedGoodsInboundSnapshotBuilder builder, MesWeekCodeRuleProvider provider, IWeekCodeResolver resolver)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder)); _provider = provider ?? throw new ArgumentNullException(nameof(provider)); _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }
        public OperationResult<MesFinishedGoodsInboundWeekCodePreviewResult> Preview(MesFinishedGoodsInboundSourceGraph source)
        {
            OperationResult<FinishedGoodsInboundReceiveRequest> built = _builder.Build(source);
            if (!built.IsSuccess) return OperationResult<MesFinishedGoodsInboundWeekCodePreviewResult>.Fail(built.Message);
            MesFinishedGoodsInboundWeekCodePreviewResult result = new MesFinishedGoodsInboundWeekCodePreviewResult { Request = built.Data };
            foreach (FinishedGoodsInboundBoxSnapshot box in built.Data.Snapshot.Boxes)
            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                AddPack(result, built.Data.Snapshot.PlantCode, box, pack);
            return OperationResult<MesFinishedGoodsInboundWeekCodePreviewResult>.Success(result);
        }
        private void AddPack(MesFinishedGoodsInboundWeekCodePreviewResult summary, string plantCode, FinishedGoodsInboundBoxSnapshot box, FinishedGoodsInboundPackSnapshot pack)
        {
            MesFinishedGoodsInboundWeekCodePackPreview preview = new MesFinishedGoodsInboundWeekCodePackPreview { BoxSourceEntityId = box.SourceEntityId, PackSourceEntityId = pack.SourceEntityId, BoxCode = box.BoxCode, PackCode = pack.PackCode, MaterialCode = box.MaterialCode, MaterialRevision = box.MaterialRevision, DateCode = pack.DateCode, Status = "UNRESOLVED" };
            try
            {
                MesWeekCodeRuleLookupResult rule = _provider.Resolve(plantCode, box.MaterialCode, box.MaterialRevision);
                preview.RuleSource = rule.RuleSource; preview.RuleValue = rule.RuleValue;
                if (!rule.IsSuccess) { preview.ErrorCode = rule.ErrorCode; preview.ErrorMessage = rule.Message; }
                else
                {
                    OperationResult<WeekCodeSnapshot> resolved = _resolver.Resolve(pack.DateCode, rule.RuleValue, pack.PackCreatedTime);
                    if (!resolved.IsSuccess) { preview.ErrorCode = ErrorCode(resolved.Message); preview.ErrorMessage = resolved.Message; }
                    else { preview.Status = "RESOLVED"; preview.WeekYear = resolved.Data.WeekYear; preview.WeekNo = resolved.Data.WeekNo; preview.WeekSortKey = resolved.Data.WeekSortKey; }
                }
            }
            catch (Exception ex) { preview.ErrorCode = "WEEKCODE_PREVIEW_ERROR"; preview.ErrorMessage = ex.Message; }
            summary.Packs.Add(preview);
            if (preview.Status == "RESOLVED") summary.ResolvedPackCount++; else { summary.UnresolvedPackCount++; Count(summary, preview.ErrorCode); }
        }
        private static string ErrorCode(string message) { int index = message == null ? -1 : message.IndexOf(':'); return index > 0 ? message.Substring(0, index) : message; }
        private static void Count(MesFinishedGoodsInboundWeekCodePreviewResult result, string code)
        {
            if (code == "WEEK_RULE_NOT_FOUND") result.RuleNotFoundCount++; else if (code == "WEEK_RULE_AMBIGUOUS") result.RuleAmbiguousCount++; else if (code == "UNSUPPORTED_WEEK_RULE") result.UnsupportedRuleCount++; else if (code == "INVALID_DATECODE") result.InvalidDateCodeCount++; else if (code == "INVALID_WEEK") result.InvalidWeekCount++; else if (code == "WEEK_ANCHOR_REQUIRED") result.AnchorRequiredCount++;
        }
    }
}
