namespace WMS_HU_Model.Models.Putaway
{
    public class PutawayPersistenceResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public PutawayResultDto Data { get; set; }


        public static PutawayPersistenceResult Success(
            PutawayResultDto data)
        {
            return new PutawayPersistenceResult
            {
                IsSuccess = true,
                Message = "success",
                Data = data
            };
        }


        public static PutawayPersistenceResult Fail(
            string message)
        {
            return new PutawayPersistenceResult
            {
                IsSuccess = false,
                Message = message,
                Data = null
            };
        }
    }
}