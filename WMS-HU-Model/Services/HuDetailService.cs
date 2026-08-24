using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class HuDetailService : IHuDetailService
    {
        private const int DefaultHistoryTop = 50;
        private const int MaxHistoryTop = 200;
        private readonly IHuRepository _huRepository;
        private readonly IHuLocationService _locationService;
        private readonly IHuDetailRepository _detailRepository;

        public HuDetailService(IHuRepository huRepository, IHuLocationService locationService, IHuDetailRepository detailRepository)
        {
            _huRepository = huRepository ?? throw new ArgumentNullException(nameof(huRepository));
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _detailRepository = detailRepository ?? throw new ArgumentNullException(nameof(detailRepository));
        }

        public OperationResult<HuDetailDto> GetDetail(string huCode)
        {
            HuDto hu;
            string error = FindHu(huCode, out hu);
            if (error != null) return OperationResult<HuDetailDto>.Fail(error);

            HuDto parent = _huRepository.GetActiveParent(hu.Id);
            HuDto root = parent ?? hu;
            if (parent != null)
            {
                HuDto grandParent = _huRepository.GetActiveParent(parent.Id);
                if (grandParent != null) root = grandParent;
            }

            List<HuTreeFlatRow> tree = _huRepository.GetTree(root.Id);
            OperationResult<HuLocationDto> location = _locationService.GetLocation(hu.HuCode);
            if (!location.IsSuccess) return OperationResult<HuDetailDto>.Fail(location.Message);

            HuDetailDto detail = new HuDetailDto
            {
                RootHuCode = root.HuCode,
                Hu = hu,
                Parent = parent == null ? null : Relation(parent),
                Tree = tree,
                Location = location.Data,
                CurrentHold = _detailRepository.GetActiveHold(hu.Id),
                Contents = _detailRepository.GetContents(hu.Id, hu.HuType),
                Children = tree.Where(x => x.ParentHuId == hu.Id).Select(Relation).ToList()
            };

            return OperationResult<HuDetailDto>.Success(detail);
        }

        public OperationResult<List<HuOperationLogDto>> GetOperationLogs(string huCode, int top)
        {
            HuDto hu;
            string error = FindHu(huCode, out hu);
            if (error != null) return OperationResult<List<HuOperationLogDto>>.Fail(error);
            return OperationResult<List<HuOperationLogDto>>.Success(_detailRepository.GetOperationLogs(hu.Id, NormalizeTop(top)));
        }

        public OperationResult<List<HuHoldLogDto>> GetHoldLogs(string huCode, int top)
        {
            HuDto hu;
            string error = FindHu(huCode, out hu);
            if (error != null) return OperationResult<List<HuHoldLogDto>>.Fail(error);
            return OperationResult<List<HuHoldLogDto>>.Success(_detailRepository.GetHoldLogs(hu.Id, NormalizeTop(top)));
        }

        private string FindHu(string huCode, out HuDto hu)
        {
            hu = null;
            if (string.IsNullOrWhiteSpace(huCode)) return "HUCode不能为空。";
            string normalized = huCode.Trim().ToUpperInvariant();
            if (normalized.Length > 100) return "HUCode长度不能超过100。";
            hu = _huRepository.GetByCode(normalized);
            return hu == null ? "HU不存在。" : null;
        }

        private static int NormalizeTop(int top)
        {
            if (top <= 0) return DefaultHistoryTop;
            return Math.Min(top, MaxHistoryTop);
        }

        private static HuDetailRelationDto Relation(HuDto hu)
        {
            return new HuDetailRelationDto
            {
                HuId = hu.Id,
                HuCode = hu.HuCode,
                HuType = hu.HuType,
                LifecycleStatus = hu.LifecycleStatus,
                IsHold = hu.IsHold
            };
        }

        private static HuDetailRelationDto Relation(HuTreeFlatRow hu)
        {
            return new HuDetailRelationDto
            {
                HuId = hu.HuId,
                HuCode = hu.HuCode,
                HuType = hu.HuType,
                LifecycleStatus = hu.LifecycleStatus,
                IsHold = hu.IsHold
            };
        }
    }
}
