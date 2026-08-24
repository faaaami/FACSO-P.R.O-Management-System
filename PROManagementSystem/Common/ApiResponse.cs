namespace PROManagementSystem.Api.Common;

public interface IApiResponse
{
    int StatusCode { get; set; }
}

public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; set; }

    public int StatusCode { get; set; }

    public T? Data { get; set; }

    public object? Meta { get; set; }

    public ApiError? Error { get; set; }

    public static ApiResponse<T> SuccessResponse(
        T data,
        object? meta = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Meta = meta,
            Error = null
        };
    }

    public static ApiResponse<T> FailureResponse(
        string code,
        string message,
        object? details = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Meta = null,
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details
            }
        };
    }
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public object? Details { get; set; }
}