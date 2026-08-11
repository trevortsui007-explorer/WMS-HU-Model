namespace WMS_HU_Model.Models.Repack
{
    public class PackInnerPersistenceResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public PackInnerResultDto Data { get; set; }


        public static PackInnerPersistenceResult Success(
            PackInnerResultDto data)
        {
            return new PackInnerPersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }


        public static PackInnerPersistenceResult Fail(
            string message)
        {
            return new PackInnerPersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}