using System;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.WeekCode;

namespace WMS_HU_Model.Services
{
    public class WeekCodeResolver : IWeekCodeResolver
    {
        private readonly IWeekCodeRuleProvider _provider;

        public WeekCodeResolver(IWeekCodeRuleProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public OperationResult<WeekCodeSnapshot> Resolve(string materialCode, string materialRevision, string dateCode)
        {
            if (string.IsNullOrWhiteSpace(dateCode))
                return OperationResult<WeekCodeSnapshot>.Success(null);

            OperationResult<WeekCodeSnapshot> result = _provider.Resolve(materialCode, materialRevision, dateCode);

            if (!result.IsSuccess || result.Data == null)
                return OperationResult<WeekCodeSnapshot>.Fail(result.Message ?? "周码解析失败。");

            WeekCodeSnapshot snapshot = result.Data;

            if (snapshot.WeekYear < 2000 || snapshot.WeekYear > 2199)
                return OperationResult<WeekCodeSnapshot>.Fail("WeekYear必须在2000到2199之间。");

            if (snapshot.WeekNo < 1 || snapshot.WeekNo > 53)
                return OperationResult<WeekCodeSnapshot>.Fail("WeekNo必须在1到53之间。");

            if (snapshot.WeekSortKey != snapshot.WeekYear * 100 + snapshot.WeekNo)
                return OperationResult<WeekCodeSnapshot>.Fail("WeekSortKey与WeekYear/WeekNo不一致。");

            if (string.IsNullOrWhiteSpace(snapshot.WeekFormatCode) || string.IsNullOrWhiteSpace(snapshot.WeekRuleSource))
                return OperationResult<WeekCodeSnapshot>.Fail("周码规则快照不完整。");

            if (snapshot.WeekResolvedTime == default(DateTime))
                snapshot.WeekResolvedTime = DateTime.Now;

            return OperationResult<WeekCodeSnapshot>.Success(snapshot);
        }
    }
}
