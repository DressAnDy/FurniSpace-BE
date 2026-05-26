#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FurniSpace.Application.Common;

public class ServiceResult : IServiceResult
{
    public int Status { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Errors { get; set; }

    public ServiceResult()
    {
        Status = -1;
        Message = "Action failed";
    }

    public ServiceResult(int status, string message)
    {
        Status = status;
        Message = message;
    }

    public ServiceResult(int status, string message, object data)
    {
        Status = status;
        Message = message;
        Data = data;
    }

    public ServiceResult(int status, string message, List<string> errors)
    {
        Status = status;
        Message = message;
        Errors = errors;
    }

    public static ServiceResult Success(string message = "Success")
    {
        return new ServiceResult(200, message);
    }

    public static ServiceResult Success(object data, string message = "Success")
    {
        return new ServiceResult(200, message, data);
    }

    public static ServiceResult Created(object data, string message = "Resource created successfully")
    {
        return new ServiceResult(201, message, data);
    }

    public static ServiceResult BadRequest(string message = "Bad request")
    {
        return new ServiceResult(400, message);
    }

    public static ServiceResult BadRequest(List<string> errors, string message = "Validation failed")
    {
        return new ServiceResult(400, message, errors);
    }

    public static ServiceResult NotFound(string message = "Resource not found")
    {
        return new ServiceResult(404, message);
    }

    public static ServiceResult Unauthorized(string message = "Unauthorized")
    {
        return new ServiceResult(401, message);
    }

    public static ServiceResult Forbidden(string message = "Forbidden")
    {
        return new ServiceResult(403, message);
    }

    public static ServiceResult InternalServerError(string message = "Internal server error")
    {
        return new ServiceResult(500, message);
    }

    public static ServiceResult Conflict(string message = "Resource conflict")
    {
        return new ServiceResult(409, message);
    }

    public static ServiceResult Failure(Error error)
    {
        return new ServiceResult(error.Status, error.Message);
    }
}

public class ServiceResult<T> : IServiceResult
{
    public int Status { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Errors { get; set; }

    object? IServiceResult.Data
    {
        get => Data;
        set => Data = (T?)value;
    }

    public ServiceResult()
    {
        Status = -1;
        Message = "Action failed";
    }

    public ServiceResult(int status, string message)
    {
        Status = status;
        Message = message;
    }

    public ServiceResult(int status, string message, T data)
    {
        Status = status;
        Message = message;
        Data = data;
    }

    public ServiceResult(int status, string message, List<string> errors)
    {
        Status = status;
        Message = message;
        Errors = errors;
    }

    public static ServiceResult<T> Success(T data, string message = "Success")
    {
        return new ServiceResult<T>(200, message, data);
    }

    public static ServiceResult<T> Created(T data, string message = "Resource created successfully")
    {
        return new ServiceResult<T>(201, message, data);
    }

    public static ServiceResult<T> BadRequest(string message = "Bad request")
    {
        return new ServiceResult<T>(400, message);
    }

    public static ServiceResult<T> BadRequest(List<string> errors, string message = "Validation failed")
    {
        return new ServiceResult<T>(400, message, errors);
    }

    public static ServiceResult<T> NotFound(string message = "Resource not found")
    {
        return new ServiceResult<T>(404, message);
    }

    public static ServiceResult<T> Unauthorized(string message = "Unauthorized")
    {
        return new ServiceResult<T>(401, message);
    }

    public static ServiceResult<T> Forbidden(string message = "Forbidden")
    {
        return new ServiceResult<T>(403, message);
    }

    public static ServiceResult<T> InternalServerError(string message = "Internal server error")
    {
        return new ServiceResult<T>(500, message);
    }

    public static ServiceResult<T> Conflict(string message = "Resource conflict")
    {
        return new ServiceResult<T>(409, message);
    }

    public static ServiceResult<T> Failure(Error error)
    {
        return new ServiceResult<T>(error.Status, error.Message);
    }
}
