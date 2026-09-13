namespace FurniSpace.Application.DTOs.Proposals;

public sealed class UpdateProposalItemRequestDto
{
    public int Quantity { get; set; }
    public string? CustomizationNote { get; set; }
}
