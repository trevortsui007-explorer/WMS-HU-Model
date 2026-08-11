namespace WMS_HU_Model.Models.Repack
{
    public class UnpackInnerPersistenceResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public UnpackInnerResultDto Data { get; set; }


        public static UnpackInnerPersistenceResult Success(
            UnpackInnerResultDto data)
        {
            return new UnpackInnerPersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }


        public static UnpackInnerPersistenceResult Fail(
            string message)
        {
            return new UnpackInnerPersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}