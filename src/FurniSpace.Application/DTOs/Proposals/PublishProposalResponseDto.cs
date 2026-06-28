using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class PublishProposalResponseDto
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public ProposalStatus? ProposalStatus { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }
    public DateTime PublishedAt { get; set; }
}
