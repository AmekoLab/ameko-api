namespace FPTU.Capstone.AMKCollective.Application.DTOs.Common
{
    /// <summary>
    /// Mẫu response chuẩn cho tất cả API
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của data</typeparam>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        /// <summary>
        /// Response thành công với data
        /// </summary>
        public static ApiResponse<T> SuccessResponse(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Errors = null
            };
        }

        /// <summary>
        /// Response thành công không có data
        /// </summary>
        public static ApiResponse<T> SuccessResponse(string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = default,
                Errors = null
            };
        }

        /// <summary>
        /// Response lỗi với message
        /// </summary>
        public static ApiResponse<T> ErrorResponse(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default,
                Errors = errors
            };
        }

        /// <summary>
        /// Response lỗi với nhiều errors
        /// </summary>
        public static ApiResponse<T> ErrorResponse(List<string> errors)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = "One or more errors occurred",
                Data = default,
                Errors = errors
            };
        }
    }
}
