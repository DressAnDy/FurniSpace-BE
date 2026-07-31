using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Proposals;

public sealed class ProposalProjectAreaReadModel
{
    public Guid ProjectAreaId { get; set; }
    public Guid ProjectId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public ProjectAreaType? AreaType { get; set; }
    public int? FloorNumber { get; set; }
    public ProjectAreaStatus? Status { get; set; }
}
