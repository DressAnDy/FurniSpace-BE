using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class RequestProposalRevisionResponseDto
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
    public string? RevisionNote { get; set; }
    public DateTime RequestedAt { get; set; }
}
