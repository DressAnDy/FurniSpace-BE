#nullable enable

namespace FurniSpace.Application.Common;

public sealed class Error
{
    public string Code { get; }
    public string Message { get; }
    public int Status { get; }

    private Error(string code, string message, int status)
    {
        Code = code;
        Message = message;
        Status = status;
    }

    public static Error Validation(string code, string message)
    {
        return new Error(code, message, 400);
    }

    public static Error BadRequest(string code, string message)
    {
        return new Error(code, message, 400);
    }

    public static Error Unauthorized(string code, string message)
    {
        return new Error(code, message, 401);
    }

    public static Error Forbidden(string code, string message)
    {
        return new Error(code, message, 403);
    }

    public static Error NotFound(string code, string message)
    {
        return new Error(code, message, 404);
    }

    public static Error Conflict(string code, string message)
    {
        return new Error(code, message, 409);
    }

    public static Error InternalServerError(string code, string message)
    {
        return new Error(code, message, 500);
    }
}
