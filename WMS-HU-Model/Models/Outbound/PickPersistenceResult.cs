namespace WMS_HU_Model.Models.Outbound
{
    public class PickPersistenceResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public PickResultDto Data { get; set; }

        public static PickPersistenceResult Success(
            PickResultDto data)
        {
            return new PickPersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }

        public static PickPersistenceResult Fail(
            string message)
        {
            return new PickPersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}
