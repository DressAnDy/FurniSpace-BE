namespace FurniSpace.Application.DTOs.Proposals;

public sealed class CreateProposalRequestDto
{
    public string ProposalName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
