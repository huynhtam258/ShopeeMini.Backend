namespace ShopeeMini.Application.Common.Exceptions;

/// <summary>Base type for exceptions the global middleware maps to a specific HTTP status code.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entity, object key) : base($"{entity} với id '{key}' không tồn tại.") { }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.") : base(message) { }
}

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Thông tin xác thực không hợp lệ.") : base(message) { }
}

public class ValidationAppException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IDictionary<string, string[]> errors) : base("Dữ liệu đầu vào không hợp lệ.")
    {
        Errors = errors;
    }
}
