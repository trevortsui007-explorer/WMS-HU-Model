using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class WeekCodeRecalculateService : IWeekCodeRecalculateService
    {
        private readonly IWeekCodeRecalculateRepository _repository;
        private readonly MesWeekCodeRuleProvider _provider;
        private readonly IWeekCodeResolver _resolver;
        private readonly IMesFinishedGoodsInboundRepository _mesRepository;

        public WeekCodeRecalculateService(
            IWeekCodeRecalculateRepository repository,
            MesWeekCodeRuleProvider provider,
            IWeekCodeResolver resolver,
            IMesFinishedGoodsInboundRepository mesRepository)
        {
            _repository = repository;
            _provider = provider;
            _resolver = resolver;
            _mesRepository = mesRepository;
        }

        public OperationResult<WeekCodeRecalculateResultDto> Recalculate(WeekCodeRecalculateRequest request)
        {
            string error = Validate(request);
            if (error != null)
            {
                return OperationResult<WeekCodeRecalculateResultDto>.Fail(error);
            }

            MesWeekCodeRuleLookupResult rule = _provider.Resolve(request.PlantCode, request.MaterialCode, request.MaterialRevision);
            if (!rule.IsSuccess)
            {
                return OperationResult<WeekCodeRecalculateResultDto>.Fail(rule.ErrorCode + ": " + rule.Message);
            }

            List<WeekCodeRecalculateBoxFact> boxes = _repository.GetCurrentBoxes(request.PlantCode, request.MaterialCode, request.MaterialRevision);
            List<string> sourceIds = boxes.SelectMany(x => x.Items)
                .Where(x => x.PackSourceSystem == "MES")
                .Select(x => x.PackSourceEntityId)
                .ToList();
            Dictionary<string, DateTime?> createdTimes = _mesRepository.GetPackCreatedTimes(sourceIds);
            WeekCodeRecalculateResultDto result = new WeekCodeRecalculateResultDto
            {
                PlantCode = request.PlantCode,
                MaterialCode = request.MaterialCode,
                MaterialRevision = request.MaterialRevision,
                RequestId = request.RequestId,
                RuleSource = rule.RuleSource,
                RuleValue = rule.RuleValue,
                BoxCount = boxes.Count
            };

            foreach (WeekCodeRecalculateBoxFact box in boxes)
            {
                WeekCodeRecalculateBoxPlan plan = new WeekCodeRecalculateBoxPlan
                {
                    BoxHUId = box.BoxHUId,
                    BoxCode = box.BoxCode,
                    BeforeIsHold = box.IsHold
                };
                string failure = null;
                foreach (WeekCodeRecalculateItemFact item in box.Items)
                {
                    DateTime? anchor = null;
                    if (item.PackSourceEntityId != null && createdTimes.ContainsKey(item.PackSourceEntityId))
                    {
                        anchor = createdTimes[item.PackSourceEntityId];
                    }

                    OperationResult<WeekCodeSnapshot> resolved = _resolver.Resolve(item.DateCode, rule.RuleValue, anchor);
                    if (!resolved.IsSuccess)
                    {
                        failure = resolved.Message;
                        break;
                    }

                    resolved.Data.WeekRuleSource = rule.RuleSource;
                    resolved.Data.WeekRuleValue = rule.RuleValue;
                    plan.Items.Add(new WeekCodeRecalculateItemPlan
                    {
                        ItemId = item.ItemId,
                        DateCode = item.DateCode,
                        OldRuleSource = item.OldRuleSource,
                        OldRuleValue = item.OldRuleValue,
                        OldWeekFormatCode = item.OldWeekFormatCode,
                        OldWeekYear = item.OldWeekYear,
                        OldWeekNo = item.OldWeekNo,
                        OldWeekSortKey = item.OldWeekSortKey,
                        WeekCode = resolved.Data
                    });
                }

                WeekCodeRecalculateBoxResultDto boxResult;
                if (failure != null)
                {
                    boxResult = Failed(box, Code(failure), failure);
                }
                else if (plan.Items.All(x => Same(box.Items.First(y => y.ItemId == x.ItemId), x.WeekCode)))
                {
                    boxResult = Unchanged(box, plan);
                }
                else
                {
                    try
                    {
                        boxResult = _repository.ApplyBox(plan, request);
                    }
                    catch (Exception ex)
                    {
                        boxResult = Failed(box, "PERSISTENCE_FAILED", ex.Message);
                    }
                }

                result.Items.Add(boxResult);
                if (boxResult.Result == "UPDATED")
                {
                    result.UpdatedBoxCount++;
                    result.UpdatedItemCount += boxResult.UpdatedItemCount;
                    if (boxResult.ReleasedWeekCodeHold)
                    {
                        result.ReleasedHoldCount++;
                    }
                }
                else if (boxResult.Result == "UNCHANGED")
                {
                    result.UnchangedBoxCount++;
                }
                else
                {
                    result.FailedBoxCount++;
                }
            }

            return OperationResult<WeekCodeRecalculateResultDto>.Success(result);
        }

        private static WeekCodeRecalculateBoxResultDto Unchanged(WeekCodeRecalculateBoxFact box, WeekCodeRecalculateBoxPlan plan)
        {
            return new WeekCodeRecalculateBoxResultDto
            {
                BoxHUId = box.BoxHUId,
                BoxCode = box.BoxCode,
                Result = "UNCHANGED",
                BeforeIsHold = box.IsHold,
                AfterIsHold = box.IsHold,
                ItemResults = plan.Items.Select(x => ResultItem(x, x.WeekCode, "UNCHANGED")).ToList()
            };
        }

        private static WeekCodeRecalculateBoxResultDto Failed(WeekCodeRecalculateBoxFact box, string errorCode, string errorMessage)
        {
            return new WeekCodeRecalculateBoxResultDto
            {
                BoxHUId = box.BoxHUId,
                BoxCode = box.BoxCode,
                Result = "FAILED",
                BeforeIsHold = box.IsHold,
                AfterIsHold = null,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                ItemResults = box.Items.Select(x => new WeekCodeRecalculateItemResultDto
                {
                    DateCode = x.DateCode,
                    OldWeekRuleSource = x.OldRuleSource,
                    OldWeekRuleValue = x.OldRuleValue,
                    OldWeekFormatCode = x.OldWeekFormatCode,
                    OldWeekYear = x.OldWeekYear,
                    OldWeekNo = x.OldWeekNo,
                    OldWeekSortKey = x.OldWeekSortKey,
                    Result = "FAILED"
                }).ToList()
            };
        }

        internal static WeekCodeRecalculateItemResultDto ResultItem(WeekCodeRecalculateItemPlan item, WeekCodeSnapshot after, string result)
        {
            return new WeekCodeRecalculateItemResultDto
            {
                DateCode = item.DateCode,
                OldWeekRuleSource = item.OldRuleSource,
                OldWeekRuleValue = item.OldRuleValue,
                OldWeekFormatCode = item.OldWeekFormatCode,
                OldWeekYear = item.OldWeekYear,
                OldWeekNo = item.OldWeekNo,
                OldWeekSortKey = item.OldWeekSortKey,
                NewWeekRuleSource = after == null ? null : after.WeekRuleSource,
                NewWeekRuleValue = after == null ? null : after.WeekRuleValue,
                NewWeekFormatCode = after == null ? null : after.WeekFormatCode,
                NewWeekYear = after == null ? (short?)null : after.WeekYear,
                NewWeekNo = after == null ? (byte?)null : after.WeekNo,
                NewWeekSortKey = after == null ? (int?)null : after.WeekSortKey,
                Result = result
            };
        }

        private static bool Same(WeekCodeRecalculateItemFact current, WeekCodeSnapshot candidate)
        {
            return current.OldRuleSource == candidate.WeekRuleSource
                && current.OldRuleValue == candidate.WeekRuleValue
                && current.OldWeekFormatCode == candidate.WeekFormatCode
                && current.OldWeekYear == candidate.WeekYear
                && current.OldWeekNo == candidate.WeekNo
                && current.OldWeekSortKey == candidate.WeekSortKey;
        }

        private static string Validate(WeekCodeRecalculateRequest request)
        {
            if (request == null)
            {
                return "请求不能为空。";
            }
            if (string.IsNullOrWhiteSpace(request.PlantCode)
                || string.IsNullOrWhiteSpace(request.MaterialCode)
                || string.IsNullOrWhiteSpace(request.MaterialRevision)
                || string.IsNullOrWhiteSpace(request.Operator)
                || string.IsNullOrWhiteSpace(request.RequestId))
            {
                return "PlantCode、MaterialCode、MaterialRevision、Operator、RequestId均不能为空。";
            }
            return null;
        }

        private static string Code(string message)
        {
            int separator = message == null ? -1 : message.IndexOf(':');
            return separator > 0 ? message.Substring(0, separator) : "PARSE_FAILED";
        }
    }
}
