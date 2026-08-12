namespace WMS_HU_Model.Models.Outbound
{
    public class PickBatchPersistenceResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public PickBatchResultDto Data { get; set; }


        public static PickBatchPersistenceResult Success(
            PickBatchResultDto data)
        {
            return new PickBatchPersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }


        public static PickBatchPersistenceResult Fail(
            string message)
        {
            return new PickBatchPersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}