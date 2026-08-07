#nullable enable

namespace FurniSpace.Application.DTOs.Production;

public sealed class AvailableProductionStaffQueryDto
{
    public Guid? ProjectId { get; set; }
    public Guid? ProductionRequestId { get; set; }
    public string? Search { get; set; }
}
