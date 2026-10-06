using System;
using WMS_HU_Model.Infrastructure.Inbound;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class FinishedGoodsInboundService : IFinishedGoodsInboundService
    {
        private readonly IFinishedGoodsInboundRepository _repository;
        private readonly IWeekCodeResolver _weekCodeResolver;
        private readonly ISapChargResolver _sapChargResolver;

        public FinishedGoodsInboundService(IFinishedGoodsInboundRepository repository, IWeekCodeResolver weekCodeResolver)
            : this(repository, weekCodeResolver, new SapChargResolver())
        {
        }

        public FinishedGoodsInboundService(IFinishedGoodsInboundRepository repository, IWeekCodeResolver weekCodeResolver, ISapChargResolver sapChargResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _weekCodeResolver = weekCodeResolver ?? throw new ArgumentNullException(nameof(weekCodeResolver));
            _sapChargResolver = sapChargResolver ?? throw new ArgumentNullException(nameof(sapChargResolver));
        }

        public OperationResult<FinishedGoodsInboundResultDto> Receive(FinishedGoodsInboundReceiveRequest request)
        {
            if (request == null) return OperationResult<FinishedGoodsInboundResultDto>.Fail("请求不能为空。");
            if (!ValidateOptional(request.RequestId, 100, "RequestId", out string requestError)
                || !ValidateRequired(request.Operator, 50, "Operator", out requestError)
                || !ValidateOptional(request.Remark, 500, "Remark", out requestError))
                return OperationResult<FinishedGoodsInboundResultDto>.Fail(requestError);
            string error = PrepareAuthoritativeLotNumbers(request.Snapshot);
            if (error != null) return OperationResult<FinishedGoodsInboundResultDto>.Fail(error);
            error = FinishedGoodsInboundSnapshotValidator.ValidateStructural(request.Snapshot);
            if (error != null) return OperationResult<FinishedGoodsInboundResultDto>.Fail(error);
            error = ResolveSapBatchNumbers(request.Snapshot);
            if (error != null) return OperationResult<FinishedGoodsInboundResultDto>.Fail(error);
            FinishedGoodsInboundCommand command = new FinishedGoodsInboundCommand
            {
                Snapshot = request.Snapshot,
                SnapshotHash = FinishedGoodsInboundSnapshotHasher.CreateHash(request.Snapshot),
                HashVersion = FinishedGoodsInboundSnapshotHasher.GetHashVersion(request.Snapshot),
                RequestId = request.RequestId,
                Operator = request.Operator.Trim(),
                Remark = request.Remark,
                ReceiveTime = DateTime.Now
            };
            FinishedGoodsInboundPersistenceResult existing = _repository.FindExisting(command);
            if (existing != null)
            {
                if (existing.IsConflict) return OperationResult<FinishedGoodsInboundResultDto>.Fail(existing.Message);
                if (existing.Data != null) return OperationResult<FinishedGoodsInboundResultDto>.Success(existing.Data, existing.Message);
            }
            foreach (FinishedGoodsInboundBoxSnapshot box in request.Snapshot.Boxes)
            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
            {
                OperationResult<WeekCodeSnapshot> result = _weekCodeResolver.Resolve(new WeekCodeResolveContext
                {
                    PlantCode = request.Snapshot.PlantCode, MaterialCode = box.MaterialCode, MaterialRevision = box.MaterialRevision,
                    RuleSource = box.WeekRuleSource, RuleValue = box.WeekRuleValue, DateCode = pack.DateCode, PackCreatedTime = pack.PackCreatedTime,
                    RuleFailureCode = box.WeekRuleFailureCode, RuleFailureMessage = box.WeekRuleFailureMessage
                });
                pack.WeekResolutionSucceeded = result.IsSuccess;
                if (result.IsSuccess)
                {
                    pack.WeekCode = result.Data;
                }
                else
                {
                    SetWeekFailure(pack, ExtractFailureCode(result.Message), result.Message, box.WeekRuleValue);
                }
            }
            error = FinishedGoodsInboundSnapshotValidator.ValidatePrepared(request.Snapshot);
            if (error != null) return OperationResult<FinishedGoodsInboundResultDto>.Fail(error);
            FinishedGoodsInboundPersistenceResult persistence;
            try
            {
                persistence = _repository.Persist(command);
            }
            catch (InboundMappingException ex)
            {
                return OperationResult<FinishedGoodsInboundResultDto>.Fail(ex.Message);
            }
            return persistence.IsConflict
                ? OperationResult<FinishedGoodsInboundResultDto>.Fail(persistence.Message)
                : OperationResult<FinishedGoodsInboundResultDto>.Success(persistence.Data, persistence.Message);
        }

        private string ResolveSapBatchNumbers(FinishedGoodsInboundSnapshot snapshot)
        {
            foreach (FinishedGoodsInboundBoxSnapshot box in snapshot.Boxes)
            foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
            {
                pack.SapBatchNo = null;
                if (string.IsNullOrWhiteSpace(pack.PackId)) continue;
                try
                {
                    pack.SapBatchNo = _sapChargResolver.Resolve(snapshot.PlantCode, pack.PackId, pack.IsSpecialSapCharg);
                }
                catch (ArgumentException ex)
                {
                    return "SAP_CHARG_INVALID: InnerPack[" + pack.PackCode + "]" + ex.Message;
                }
            }
            return null;
        }

        private static string PrepareAuthoritativeLotNumbers(FinishedGoodsInboundSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Boxes == null) return null;
            foreach (FinishedGoodsInboundBoxSnapshot box in snapshot.Boxes)
            {
                if (box == null || box.InnerPacks == null) continue;
                foreach (FinishedGoodsInboundPackSnapshot pack in box.InnerPacks)
                {
                    if (pack == null) continue;
                    if (!pack.HasAuthoritativeMesLotNo)
                    {
                        pack.LotNo = null;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(pack.LotNo))
                        return "MES_LOTNO_INVALID: InnerPack[" + pack.PackCode + "]缺少有效LotNo。";
                    if (pack.LotNo.Trim().Length > 500)
                        return "MES_LOTNO_TOO_LONG: InnerPack[" + pack.PackCode + "]拼接后的LotNo长度不能超过500。";
                    pack.LotNo = pack.LotNo.Trim();
                }
            }
            return null;
        }

        private static bool ValidateOptional(string value, int maxLength, string fieldName, out string error)
        {
            error = value != null && value.Trim().Length > maxLength ? fieldName + "长度不能超过" + maxLength + "。" : null;
            return error == null;
        }

        private static bool ValidateRequired(string value, int maxLength, string fieldName, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = fieldName + "不能为空。";
                return false;
            }
            return ValidateOptional(value, maxLength, fieldName, out error);
        }

        private static string ExtractFailureCode(string message)
        {
            int separator = message == null ? -1 : message.IndexOf(':');
            return separator > 0 ? message.Substring(0, separator) : "WEEKCODE_UNRESOLVED";
        }

        private static void SetWeekFailure(FinishedGoodsInboundPackSnapshot pack, string rawCode, string rawMessage, string ruleValue)
        {
            string code = NormalizeFailureCode(rawCode, ruleValue);
            string detail = ExtractFailureDetail(rawMessage, rawCode);
            if (code == "WEEK_RULE_MANUAL_REQUIRED") detail = "MES周期格式为“见备注”，请到MES指示库维护明确的周期格式。";
            else if (code == "WEEK_RULE_INVALID_CONFIG") detail = "MES周期格式“" + (ruleValue ?? string.Empty).Trim() + "”不是可解析规则，请到MES指示库维护。";
            else if (string.IsNullOrWhiteSpace(detail)) detail = DefaultFailureDetail(code);
            pack.WeekResolutionSucceeded = false;
            pack.WeekFailureCode = code;
            pack.WeekFailureMessage = string.IsNullOrWhiteSpace(detail) ? code : code + ": " + detail;
            pack.WeekCode = null;
        }

        private static string NormalizeFailureCode(string rawCode, string ruleValue)
        {
            if ((rawCode ?? "").StartsWith("PACK_WEEKCODE_", StringComparison.Ordinal)) return rawCode;
            string rule = string.IsNullOrWhiteSpace(ruleValue) ? null : ruleValue.Trim();
            if (string.Equals(rule, "见备注", StringComparison.OrdinalIgnoreCase) && (rawCode == "UNSUPPORTED_WEEK_RULE" || rawCode == "WEEK_RULE_UNSUPPORTED")) return "WEEK_RULE_MANUAL_REQUIRED";
            if ((string.Equals(rule, "N", StringComparison.OrdinalIgnoreCase) || string.Equals(rule, "/", StringComparison.Ordinal))
                && rawCode == "UNSUPPORTED_WEEK_RULE") return "WEEK_RULE_INVALID_CONFIG";
            string code = string.IsNullOrWhiteSpace(rawCode) ? "WEEKCODE_UNRESOLVED" : rawCode.Trim().ToUpperInvariant();
            if (code == "WEEK_RULE_MISSING") return "WEEK_RULE_NOT_FOUND";
            if (code == "UNSUPPORTED_WEEK_RULE") return "WEEK_RULE_UNSUPPORTED";
            return code;
        }

        private static string ExtractFailureDetail(string message, string rawCode)
        {
            if (string.IsNullOrWhiteSpace(message)) return null;
            string value = message.Trim();
            int separator = value.IndexOf(':');
            if (separator <= 0) return string.Equals(value, rawCode, StringComparison.OrdinalIgnoreCase) ? null : value;
            string prefix = value.Substring(0, separator).Trim();
            return string.Equals(prefix, rawCode, StringComparison.OrdinalIgnoreCase) ? value.Substring(separator + 1).Trim() : value;
        }

        private static string DefaultFailureDetail(string code)
        {
            if (code == "WEEK_RULE_NOT_FOUND") return "MES未维护周期格式。";
            if (code == "WEEK_RULE_AMBIGUOUS") return "MES存在多个不同周期格式。";
            return null;
        }
    }
}
