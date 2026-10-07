namespace ShopeeMini.Application.Common.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public object? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Thao tác thành công") =>
        new() { Success = true, Data = data, Message = message, Errors = null };

    public static ApiResponse<T> Fail(string message, object? errors = null) =>
        new() { Success = false, Data = default, Message = message, Errors = errors };
}

public static class ApiResponse
{
    public static ApiResponse<object?> Ok(string message = "Thao tác thành công") =>
        new() { Success = true, Data = null, Message = message };

    public static ApiResponse<object?> Fail(string message, object? errors = null) =>
        new() { Success = false, Data = null, Message = message, Errors = errors };
}
