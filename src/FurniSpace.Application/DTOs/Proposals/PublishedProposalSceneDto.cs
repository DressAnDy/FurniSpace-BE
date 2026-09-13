using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class PublishedProposalSceneDto
{
    public Guid SceneId { get; set; }
    public string? SceneName { get; set; }
    public ProposalSceneType? SceneType { get; set; }
    public string? PreviewFileUrl { get; set; }
    public string RoomPlannerUrl { get; set; } = string.Empty;
}
