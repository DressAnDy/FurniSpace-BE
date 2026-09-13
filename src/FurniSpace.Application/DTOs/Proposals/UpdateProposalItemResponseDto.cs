namespace FurniSpace.Application.DTOs.Proposals;

public sealed class UpdateProposalItemResponseDto : ProposalItemSummaryDto
{
    public DateTime? UpdatedAt { get; set; }
}
