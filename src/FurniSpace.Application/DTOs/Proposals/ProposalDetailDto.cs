namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalDetailDto : ProposalDto
{
    public IReadOnlyList<ProposalSceneDto> Scenes { get; set; } = [];
    public IReadOnlyList<ProposalItemSummaryDto> Items { get; set; } = [];
}
