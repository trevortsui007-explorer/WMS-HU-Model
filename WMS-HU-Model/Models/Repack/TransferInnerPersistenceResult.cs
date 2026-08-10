namespace WMS_HU_Model.Models.Repack
{
    public class TransferInnerPersistenceResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public TransferInnerResultDto Data { get; set; }


        public static TransferInnerPersistenceResult Success(
            TransferInnerResultDto data)
        {
            return new TransferInnerPersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }


        public static TransferInnerPersistenceResult Fail(
            string message)
        {
            return new TransferInnerPersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}