using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class PublishedProposalDto
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProposalName { get; set; } = string.Empty;
    public int? VersionNo { get; set; }
    public ProposalStatus? Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public IReadOnlyList<PublishedProposalSceneDto> Scenes { get; set; } = [];
    public IReadOnlyList<ProposalItemSummaryDto> Items { get; set; } = [];
}
