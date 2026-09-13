using System;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalItemListQueryDto
{
    public Guid? SceneId { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
