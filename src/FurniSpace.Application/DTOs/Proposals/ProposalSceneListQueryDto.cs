using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalSceneListQueryDto
{
    public ProposalSceneType? SceneType { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
