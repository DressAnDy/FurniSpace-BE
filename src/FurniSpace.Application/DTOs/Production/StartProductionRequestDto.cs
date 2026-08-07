#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class StartProductionRequestDto
{
    public DateOnly? ActualStartDate { get; set; }
}
