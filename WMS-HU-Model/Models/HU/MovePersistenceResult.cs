namespace WMS_HU_Model.Models.HU
{
    public class MovePersistenceResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public HuMoveResultDto Data { get; set; }


        public static MovePersistenceResult Success(
            HuMoveResultDto data)
        {
            return new MovePersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }


        public static MovePersistenceResult Fail(
            string message)
        {
            return new MovePersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}