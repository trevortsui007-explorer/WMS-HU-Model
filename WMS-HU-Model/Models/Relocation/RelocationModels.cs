using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Relocation
{
    public class BatchRelocationRequest
    {
        public string WarehouseCode { get; set; }
        public string SourceLocationCode { get; set; }
        public string TargetLocationCode { get; set; }
        public List<string> BoxHuCodes { get; set; }
        public string RequestId { get; set; }
        public string Operator { get; set; }
        public string Reason { get; set; }
        public string Remark { get; set; }

        public BatchRelocationRequest()
        {
            BoxHuCodes = new List<string>();
        }
    }

    public class BatchRelocationCommand : BatchRelocationRequest
    {
        public string OperationNo { get; set; }
    }

    public class BatchRelocationResultDto
    {
        public string OperationNo { get; set; }
        public string SourceLocationCode { get; set; }
        public string TargetLocationCode { get; set; }
        public List<string> BoxCodes { get; set; }
        public int MovedCount { get; set; }
        public bool IsDuplicate { get; set; }
        public DateTime MoveTime { get; set; }

        public BatchRelocationResultDto()
        {
            BoxCodes = new List<string>();
        }
    }

    public class BatchRelocationPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public BatchRelocationResultDto Data { get; set; }

        public static BatchRelocationPersistenceResult Success(BatchRelocationResultDto data)
        {
            return new BatchRelocationPersistenceResult { IsSuccess = true, Data = data };
        }

        public static BatchRelocationPersistenceResult Fail(string message)
        {
            return new BatchRelocationPersistenceResult { IsSuccess = false, Message = message };
        }
    }
}
