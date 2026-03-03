namespace Sentana.API.Helpers
{
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        // Hàm tiện ích trả về thành công
        public static ApiResponse<T> Success(T data, string message = "Success")
        {
            return new ApiResponse<T> { StatusCode = 200, Message = message, Data = data };
        }

        // Hàm tiện ích trả về lỗi
        public static ApiResponse<T> Fail(int statusCode, string message)
        {
            return new ApiResponse<T> { StatusCode = statusCode, Message = message, Data = default };
        }
    }
}