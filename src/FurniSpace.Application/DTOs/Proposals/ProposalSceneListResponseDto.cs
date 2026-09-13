namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalSceneListResponseDto
{
    public IReadOnlyList<ProposalSceneDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}
