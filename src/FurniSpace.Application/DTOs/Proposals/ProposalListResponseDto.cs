namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalListResponseDto
{
    public IReadOnlyList<ProposalDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
