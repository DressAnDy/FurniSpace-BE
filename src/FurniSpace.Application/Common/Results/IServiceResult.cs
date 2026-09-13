#nullable enable

using System.Collections.Generic;

namespace FurniSpace.Application.Common;

public interface IServiceResult
{
    int Status { get; set; }
    string? Message { get; set; }
    object? Data { get; set; }
    List<string>? Errors { get; set; }
    string? ErrorCode { get; set; }
}
