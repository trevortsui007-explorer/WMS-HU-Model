using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class HuIdentifierService : IHuIdentifierService
    {
        private readonly IHuIdentifierRepository _repository;

        public HuIdentifierService(IHuIdentifierRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public OperationResult<HuIdentityDto> Resolve(string rawValue, string customerCode)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return OperationResult<HuIdentityDto>.Fail("箱码不能为空。");

            string normalizedValue = rawValue.Trim().ToUpperInvariant();
            string normalizedCustomer = string.IsNullOrWhiteSpace(customerCode)
                ? null
                : customerCode.Trim().ToUpperInvariant();

            List<HuIdentityDto> candidates = _repository.ResolveCandidates(normalizedValue, normalizedCustomer);
            HuIdentityDto direct = candidates.FirstOrDefault(x => x.MatchedHuCode);

            if (direct != null)
                return OperationResult<HuIdentityDto>.Success(direct);

            List<HuIdentityDto> distinct = candidates.GroupBy(x => x.HuId).Select(x => x.First()).ToList();

            if (distinct.Count == 0)
                return OperationResult<HuIdentityDto>.Fail("箱码未匹配到有效HU。");

            if (distinct.Count > 1)
                return OperationResult<HuIdentityDto>.Fail("箱码匹配到多个HU，请提供订单客户上下文。");

            return OperationResult<HuIdentityDto>.Success(distinct[0]);
        }
    }
}
