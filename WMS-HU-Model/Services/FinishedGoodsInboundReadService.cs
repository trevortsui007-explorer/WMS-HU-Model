using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.Inbound;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class FinishedGoodsInboundReadService : IFinishedGoodsInboundReadService
    {
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 200;
        private const int DetailBoxLimit = 50;
        private readonly IFinishedGoodsInboundReadRepository _repository;
        private readonly IEffectiveLocationResolver _effectiveLocationResolver;

        public FinishedGoodsInboundReadService(IFinishedGoodsInboundReadRepository repository, IEffectiveLocationResolver effectiveLocationResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _effectiveLocationResolver = effectiveLocationResolver ?? throw new ArgumentNullException(nameof(effectiveLocationResolver));
        }

        public OperationResult<FinishedGoodsInboundRecordResultDto> Query(FinishedGoodsInboundRecordQuery query)
        {
            if (query == null) query = new FinishedGoodsInboundRecordQuery();
            if (query.Page == 0) query.Page = 1;
            if (query.PageSize == 0) query.PageSize = DefaultPageSize;
            if (query.Page < 1) return OperationResult<FinishedGoodsInboundRecordResultDto>.Fail("Page必须大于0。");
            if (query.PageSize < 1 || query.PageSize > MaxPageSize) return OperationResult<FinishedGoodsInboundRecordResultDto>.Fail("PageSize必须在1到200之间。");
            string dateError = NormalizeDates(query);
            if (dateError != null) return OperationResult<FinishedGoodsInboundRecordResultDto>.Fail(dateError);
            NormalizeText(query);
            if (query.ExceptionCategory != null && query.ExceptionCategory != "WEEKCODE" && query.ExceptionCategory != "HOLD")
                return OperationResult<FinishedGoodsInboundRecordResultDto>.Fail("ExceptionCategory仅支持WEEKCODE或HOLD。");
            FinishedGoodsInboundRecordResultDto result = _repository.Query(query);
            return OperationResult<FinishedGoodsInboundRecordResultDto>.Success(result);
        }

        public OperationResult<FinishedGoodsInboundDetailDto> GetDetail(long receiptId)
        {
            if (receiptId <= 0) return OperationResult<FinishedGoodsInboundDetailDto>.Fail("ReceiptId无效。");
            FinishedGoodsInboundDetailDto detail = _repository.GetDetail(receiptId, DetailBoxLimit);
            if (detail == null || detail.Record == null) return OperationResult<FinishedGoodsInboundDetailDto>.Fail("入库记录不存在。");
            List<long> huIds = detail.Pallets.Select(x => x.HuId).Concat(detail.Boxes.Select(x => x.HuId)).Distinct().ToList();
            Dictionary<long, HuLocationDto> locations = _effectiveLocationResolver.ResolveMany(huIds).ToDictionary(x => x.HuId);
            foreach (FinishedGoodsInboundPalletDto pallet in detail.Pallets) ApplyLocation(pallet, locations);
            foreach (FinishedGoodsInboundBoxDto box in detail.Boxes) ApplyLocation(box, locations);
            detail.HasMoreBoxes = detail.Record.BoxCount > detail.Boxes.Count;
            return OperationResult<FinishedGoodsInboundDetailDto>.Success(detail);
        }

        private static string NormalizeDates(FinishedGoodsInboundRecordQuery query)
        {
            DateTime parsed;
            if (!string.IsNullOrWhiteSpace(query.DateFrom))
            {
                if (!DateTime.TryParseExact(query.DateFrom.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return "DateFrom格式必须为yyyy-MM-dd。";
                query.FromTime = parsed;
            }
            if (!string.IsNullOrWhiteSpace(query.DateTo))
            {
                if (!DateTime.TryParseExact(query.DateTo.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) return "DateTo格式必须为yyyy-MM-dd。";
                query.ToTimeExclusive = parsed.AddDays(1);
            }
            if (query.FromTime.HasValue && query.ToTimeExclusive.HasValue && query.FromTime.Value >= query.ToTimeExclusive.Value) return "DateFrom不能晚于DateTo。";
            return null;
        }

        private static void NormalizeText(FinishedGoodsInboundRecordQuery query)
        {
            query.PlantCode = Text(query.PlantCode, true);
            query.SourceSystem = Text(query.SourceSystem, true);
            query.SourceType = Text(query.SourceType, true);
            query.SourceEntityId = Text(query.SourceEntityId, false);
            query.PalletCode = Text(query.PalletCode, true);
            query.BoxCode = Text(query.BoxCode, true);
            query.MaterialCode = Text(query.MaterialCode, true);
            query.RequestId = Text(query.RequestId, false);
            query.ExceptionCategory = Text(query.ExceptionCategory, true);
        }

        private static string Text(string value, bool upper)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string result = value.Trim();
            return upper ? result.ToUpperInvariant() : result;
        }

        private static void ApplyLocation(FinishedGoodsInboundPalletDto pallet, IDictionary<long, HuLocationDto> locations)
        {
            HuLocationDto location;
            if (!locations.TryGetValue(pallet.HuId, out location)) return;
            pallet.DirectLocationCode = location.DirectLocationCode;
            pallet.EffectiveLocationCode = location.EffectiveLocationCode;
            pallet.IsLocationInherited = location.IsInherited;
            pallet.LocationSourceHuCode = location.LocationSourceHuCode;
        }

        private static void ApplyLocation(FinishedGoodsInboundBoxDto box, IDictionary<long, HuLocationDto> locations)
        {
            HuLocationDto location;
            if (!locations.TryGetValue(box.HuId, out location)) return;
            box.DirectLocationCode = location.DirectLocationCode;
            box.EffectiveLocationCode = location.EffectiveLocationCode;
            box.IsLocationInherited = location.IsInherited;
            box.LocationSourceHuCode = location.LocationSourceHuCode;
        }
    }
}
