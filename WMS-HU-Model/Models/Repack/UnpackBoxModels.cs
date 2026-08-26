using System;
using System.Collections.Generic;

namespace WMS_HU_Model.Models.Repack
{
    public class UnpackBoxRequest
    {
        public string BoxHuCode { get; set; }
        public string WarehouseCode { get; set; }
        public string ToLocationCode { get; set; }
        public string ScannedLocationCode { get; set; }
        public string RequestId { get; set; }
        public string BusinessNo { get; set; }
        public string Operator { get; set; }
        public string Remark { get; set; }
    }

    public class UnpackBoxCommand : UnpackBoxRequest
    {
        public string OperationNo { get; set; }
    }

    public class UnpackBoxResultDto
    {
        public string OperationNo { get; set; }
        public string BoxHuCode { get; set; }
        public string FromLocationCode { get; set; }
        public string ToLocationCode { get; set; }
        public List<string> InnerPackCodes { get; set; }
        public int UnpackedCount { get; set; }
        public bool IsDuplicate { get; set; }
        public DateTime OperationTime { get; set; }

        public UnpackBoxResultDto()
        {
            InnerPackCodes = new List<string>();
        }
    }

    public class UnpackBoxPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public UnpackBoxResultDto Data { get; set; }

        public static UnpackBoxPersistenceResult Success(UnpackBoxResultDto data)
        {
            return new UnpackBoxPersistenceResult { IsSuccess = true, Data = data };
        }

        public static UnpackBoxPersistenceResult Fail(string message)
        {
            return new UnpackBoxPersistenceResult { IsSuccess = false, Message = message };
        }
    }
}
