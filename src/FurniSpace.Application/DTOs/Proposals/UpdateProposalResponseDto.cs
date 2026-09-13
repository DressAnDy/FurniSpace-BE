using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class UpdateProposalResponseDto
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProposalName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? VersionNo { get; set; }
    public ProposalStatus? Status { get; set; }
    public DateTime UpdatedAt { get; set; }
}
