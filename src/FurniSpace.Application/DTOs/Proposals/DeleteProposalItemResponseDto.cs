namespace FurniSpace.Application.DTOs.Proposals;

public sealed class DeleteProposalItemResponseDto
{
    public Guid ProposalItemId { get; set; }
    public bool Deleted { get; set; }
}
