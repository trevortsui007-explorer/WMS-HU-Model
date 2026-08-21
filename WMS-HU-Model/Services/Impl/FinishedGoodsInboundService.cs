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

        public FinishedGoodsInboundService(IFinishedGoodsInboundRepository repository, IWeekCodeResolver weekCodeResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _weekCodeResolver = weekCodeResolver ?? throw new ArgumentNullException(nameof(weekCodeResolver));
        }

        public OperationResult<FinishedGoodsInboundResultDto> Receive(FinishedGoodsInboundReceiveRequest request)
        {
            if (request == null) return OperationResult<FinishedGoodsInboundResultDto>.Fail("请求不能为空。");
            if (!ValidateOptional(request.RequestId, 100, "RequestId", out string requestError)
                || !ValidateRequired(request.Operator, 50, "Operator", out requestError)
                || !ValidateOptional(request.Remark, 1000, "Remark", out requestError))
                return OperationResult<FinishedGoodsInboundResultDto>.Fail(requestError);
            string error = FinishedGoodsInboundSnapshotValidator.ValidateStructural(request.Snapshot);
            if (error != null) return OperationResult<FinishedGoodsInboundResultDto>.Fail(error);
            FinishedGoodsInboundCommand command = new FinishedGoodsInboundCommand
            {
                Snapshot = request.Snapshot,
                SnapshotHash = FinishedGoodsInboundSnapshotHasher.CreateHash(request.Snapshot),
                HashVersion = FinishedGoodsInboundSnapshotHasher.HashVersion,
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
                OperationResult<WeekCodeSnapshot> result = _weekCodeResolver.Resolve(pack.DateCode, box.WeekRuleValue, pack.PackCreatedTime);
                pack.WeekResolutionSucceeded = result.IsSuccess;
                if (result.IsSuccess)
                {
                    result.Data.WeekRuleSource = string.IsNullOrWhiteSpace(box.WeekRuleSource) ? null : box.WeekRuleSource.Trim();
                    result.Data.WeekRuleValue = string.IsNullOrWhiteSpace(box.WeekRuleValue) ? null : box.WeekRuleValue.Trim();
                    pack.WeekCode = result.Data;
                }
                else
                {
                    pack.WeekFailureCode = ExtractFailureCode(result.Message);
                    pack.WeekFailureMessage = result.Message;
                    pack.WeekCode = null;
                }
            }
            error = FinishedGoodsInboundSnapshotValidator.ValidatePrepared(request.Snapshot);
            if (error != null) return OperationResult<FinishedGoodsInboundResultDto>.Fail(error);
            FinishedGoodsInboundPersistenceResult persistence = _repository.Persist(command);
            return persistence.IsConflict
                ? OperationResult<FinishedGoodsInboundResultDto>.Fail(persistence.Message)
                : OperationResult<FinishedGoodsInboundResultDto>.Success(persistence.Data, persistence.Message);
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
    }
}
