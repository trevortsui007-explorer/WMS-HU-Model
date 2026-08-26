using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Common;
using WMS_HU_Model.Models.Relocation;
using WMS_HU_Model.Repositories;

namespace WMS_HU_Model.Services
{
    public class RelocationService : IRelocationService
    {
        private readonly IRelocationRepository _repository;

        public RelocationService(IRelocationRepository repository)
        {
            _repository = repository;
        }

        public OperationResult<BatchRelocationResultDto> MoveBatch(BatchRelocationRequest request)
        {
            if (request == null) return OperationResult<BatchRelocationResultDto>.Fail("请求不能为空。");

            string warehouseCode = Normalize(request.WarehouseCode);
            string sourceCode = Normalize(request.SourceLocationCode);
            string targetCode = Normalize(request.TargetLocationCode);
            string requestId = Text(request.RequestId);
            string operatorCode = Text(request.Operator);
            if (warehouseCode == null) return OperationResult<BatchRelocationResultDto>.Fail("WarehouseCode不能为空。");
            if (sourceCode == null) return OperationResult<BatchRelocationResultDto>.Fail("SourceLocationCode不能为空。");
            if (targetCode == null) return OperationResult<BatchRelocationResultDto>.Fail("TargetLocationCode不能为空。");
            if (string.Equals(sourceCode, targetCode, StringComparison.OrdinalIgnoreCase)) return OperationResult<BatchRelocationResultDto>.Fail("源货位与目标货位不能相同。");
            if (requestId == null) return OperationResult<BatchRelocationResultDto>.Fail("RequestId不能为空。");
            if (operatorCode == null) return OperationResult<BatchRelocationResultDto>.Fail("Operator不能为空。");
            if (warehouseCode.Length > 30 || sourceCode.Length > 50 || targetCode.Length > 50) return OperationResult<BatchRelocationResultDto>.Fail("仓库或货位编码长度超限。");
            if (requestId.Length > 90) return OperationResult<BatchRelocationResultDto>.Fail("RequestId长度不能超过90。");
            if (operatorCode.Length > 50) return OperationResult<BatchRelocationResultDto>.Fail("Operator长度不能超过50。");

            List<string> boxCodes = (request.BoxHuCodes ?? new List<string>()).Select(Normalize).Where(x => x != null).ToList();
            if (boxCodes.Count == 0) return OperationResult<BatchRelocationResultDto>.Fail("BoxHuCodes不能为空。");
            if (boxCodes.Any(x => x.Length > 100)) return OperationResult<BatchRelocationResultDto>.Fail("BoxHuCode长度不能超过100。");
            if (boxCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != boxCodes.Count) return OperationResult<BatchRelocationResultDto>.Fail("BoxHuCodes存在重复箱码。");
            if (boxCodes.Count > 200) return OperationResult<BatchRelocationResultDto>.Fail("单次移库最多支持200个外箱。");

            BatchRelocationResultDto duplicate = _repository.GetByRequestId(requestId);
            if (duplicate != null)
            {
                bool same = string.Equals(duplicate.SourceLocationCode, sourceCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(duplicate.TargetLocationCode, targetCode, StringComparison.OrdinalIgnoreCase)
                    && new HashSet<string>(duplicate.BoxCodes, StringComparer.OrdinalIgnoreCase).SetEquals(boxCodes);
                if (!same) return OperationResult<BatchRelocationResultDto>.Fail("RequestId已经被其他批量移库业务使用。");
                duplicate.IsDuplicate = true;
                return OperationResult<BatchRelocationResultDto>.Success(duplicate, "重复请求，返回原批量移库结果");
            }

            BatchRelocationCommand command = new BatchRelocationCommand
            {
                WarehouseCode = warehouseCode,
                SourceLocationCode = sourceCode,
                TargetLocationCode = targetCode,
                BoxHuCodes = boxCodes,
                RequestId = requestId,
                Operator = operatorCode,
                Reason = Text(request.Reason),
                Remark = Text(request.Remark),
                OperationNo = Guid.NewGuid().ToString("N").ToUpperInvariant()
            };
            BatchRelocationPersistenceResult persisted = _repository.ExecuteBatch(command);
            return persisted.IsSuccess
                ? OperationResult<BatchRelocationResultDto>.Success(persisted.Data, "批量移库成功")
                : OperationResult<BatchRelocationResultDto>.Fail(persisted.Message);
        }

        private static string Normalize(string value)
        {
            string text = Text(value);
            return text == null ? null : text.ToUpperInvariant();
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
