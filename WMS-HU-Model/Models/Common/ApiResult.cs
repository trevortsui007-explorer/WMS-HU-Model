namespace WMS_HU_Model.Models.Common
{
    public class ApiResult
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public object Data { get; set; }

        public static ApiResult Success(object data = null, string message = "success")
        {
            return new ApiResult
            {
                Code = 200,
                Message = message,
                Data = data
            };
        }

        public static ApiResult Fail(string message, int code = 400)
        {
            return new ApiResult
            {
                Code = code,
                Message = message,
                Data = null
            };
        }
    }
}