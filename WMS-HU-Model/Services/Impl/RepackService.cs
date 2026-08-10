using System;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.HU;
using WMS_HU_Model.Models.Repack;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class RepackService : IRepackService
    {
        private readonly IHuRepository _huRepository;
        private readonly IHuLocationRepository _huLocationRepository;
        private readonly IRepackRepository _repackRepository;

        public RepackService(
            IHuRepository huRepository,
            IHuLocationRepository huLocationRepository,
            IRepackRepository repackRepository)
        {
            _huRepository = huRepository;
            _huLocationRepository = huLocationRepository;
            _repackRepository = repackRepository;
        }

        public OperationResult<BoxRepackStatusDto> GetBoxStatus(string boxHuCode)
        {
            if (string.IsNullOrWhiteSpace(boxHuCode))
                return OperationResult<BoxRepackStatusDto>.Fail("BoxHuCode不能为空。");

            string normalizedCode = boxHuCode
                .Trim()
                .ToUpperInvariant();

            HuDto box = _huRepository.GetByCode(normalizedCode);

            if (box == null)
                return OperationResult<BoxRepackStatusDto>.Fail("外箱HU不存在。");

            if (box.HuType != "OUTER_BOX")
                return OperationResult<BoxRepackStatusDto>.Fail("当前HU不是OUTER_BOX。");

            BoxRepackStatusDto result = _repackRepository.GetBoxStatus(box.Id);

            return OperationResult<BoxRepackStatusDto>.Success(result);
        }
        public OperationResult<TransferInnerResultDto> TransferInner(TransferInnerRequest request)
        {
            if (request == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("请求不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.SourceBoxHuCode))
            {
                return OperationResult<TransferInnerResultDto>.Fail("SourceBoxHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.TargetBoxHuCode))
            {
                return OperationResult<TransferInnerResultDto>.Fail("TargetBoxHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.InnerHuCode))
            {
                return OperationResult<TransferInnerResultDto>.Fail("InnerHuCode不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                return OperationResult<TransferInnerResultDto>.Fail("RequestId不能为空。");
            }

            if (string.IsNullOrWhiteSpace(request.Operator))
            {
                return OperationResult<TransferInnerResultDto>.Fail("Operator不能为空。");
            }

            string sourceCode = request.SourceBoxHuCode.Trim().ToUpperInvariant();
            string targetCode = request.TargetBoxHuCode.Trim().ToUpperInvariant();
            string innerCode = request.InnerHuCode.Trim().ToUpperInvariant();

            if (sourceCode == targetCode)
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱和目标外箱不能相同。");
            }

            //----------------------------------------------------
            // 幂等
            //----------------------------------------------------
            TransferInnerResultDto duplicate = _repackRepository.GetTransferByRequestId(request.RequestId);

            if (duplicate != null)
            {
                bool sameRequest =
                    string.Equals(duplicate.SourceBoxHuCode, sourceCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(duplicate.TargetBoxHuCode, targetCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(duplicate.InnerHuCode, innerCode, StringComparison.OrdinalIgnoreCase);

                if (!sameRequest)
                {
                    return OperationResult<TransferInnerResultDto>.Fail("RequestId已经被其他Repack业务使用。");
                }

                duplicate.IsDuplicate = true;

                return OperationResult<TransferInnerResultDto>.Success(duplicate, "重复请求，返回原Repack结果");
            }

            HuDto source = _huRepository.GetByCode(sourceCode);
            HuDto target = _huRepository.GetByCode(targetCode);
            HuDto inner = _huRepository.GetByCode(innerCode);

            if (source == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱不存在。");
            }

            if (target == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("目标外箱不存在。");
            }

            if (inner == null)
            {
                return OperationResult<TransferInnerResultDto>.Fail("内包HU不存在。");
            }

            if (source.HuType != "OUTER_BOX" || target.HuType != "OUTER_BOX")
            {
                return OperationResult<TransferInnerResultDto>.Fail("源HU和目标HU必须都是OUTER_BOX。");
            }

            if (inner.HuType != "INNER_PACK")
            {
                return OperationResult<TransferInnerResultDto>.Fail("转移对象必须是INNER_PACK。");
            }

            if (source.LifecycleStatus != "ACTIVE" ||
                target.LifecycleStatus != "ACTIVE" ||
                inner.LifecycleStatus != "ACTIVE")
            {
                return OperationResult<TransferInnerResultDto>.Fail("只有ACTIVE状态HU允许Repack。");
            }

            if (source.IsHold || target.IsHold || inner.IsHold)
            {
                return OperationResult<TransferInnerResultDto>.Fail("参与Repack的HU存在冻结状态。");
            }

            if (!string.Equals(source.PlantCode, target.PlantCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(source.PlantCode, inner.PlantCode, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱、目标外箱、内包厂别必须一致。");
            }

            //----------------------------------------------------
            // Source确认包含Inner
            //----------------------------------------------------
            BoxRepackStatusDto sourceStatus = _repackRepository.GetBoxStatus(source.Id);

            bool sourceContainsInner = sourceStatus.InnerPackCodes
                .Any(x => string.Equals(x, innerCode, StringComparison.OrdinalIgnoreCase));

            if (!sourceContainsInner)
            {
                return OperationResult<TransferInnerResultDto>.Fail("内包当前不属于指定源外箱。");
            }

            //----------------------------------------------------
            // 两个Box必须有直接位置
            //----------------------------------------------------
            HuLocationDto sourceLocation = _huLocationRepository.GetLocation(source.Id);
            HuLocationDto targetLocation = _huLocationRepository.GetLocation(target.Id);
            HuLocationDto innerLocation = _huLocationRepository.GetLocation(inner.Id);

            if (sourceLocation == null || !sourceLocation.DirectLocationId.HasValue)
            {
                return OperationResult<TransferInnerResultDto>.Fail("源外箱没有直接货位。");
            }

            if (targetLocation == null || !targetLocation.DirectLocationId.HasValue)
            {
                return OperationResult<TransferInnerResultDto>.Fail("目标外箱没有直接货位。");
            }

            if (innerLocation != null && innerLocation.DirectLocationId.HasValue)
            {
                return OperationResult<TransferInnerResultDto>.Fail("当前内包已经有独立货位，不能执行箱到箱Repack。");
            }

            TransferInnerCommand command = new TransferInnerCommand
            {
                SourceBoxHuId = source.Id,
                SourceBoxHuCode = source.HuCode,
                TargetBoxHuId = target.Id,
                TargetBoxHuCode = target.HuCode,
                InnerHuId = inner.Id,
                InnerHuCode = inner.HuCode,
                PlantCode = inner.PlantCode,
                FromLocationId = sourceLocation.DirectLocationId.Value,
                FromLocationCode = sourceLocation.DirectLocationCode,
                ToLocationId = targetLocation.DirectLocationId.Value,
                ToLocationCode = targetLocation.DirectLocationCode,
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                RequestId = request.RequestId,
                BusinessNo = request.BusinessNo,
                Operator = request.Operator,
                Remark = request.Remark
            };

            TransferInnerPersistenceResult persistResult = _repackRepository.ExecuteTransferInner(command);

            if (!persistResult.IsSuccess)
            {
                return OperationResult<TransferInnerResultDto>.Fail(persistResult.Message);
            }

            return OperationResult<TransferInnerResultDto>.Success(persistResult.Data, "内包换箱成功");
        }
    }
}