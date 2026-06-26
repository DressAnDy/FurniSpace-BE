using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class CreateProposalSceneRequestDto
{
    public string? SceneName { get; set; }
    public ProposalSceneType? SceneType { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public string? MongoSceneId { get; set; }
    public Guid? PreviewFileId { get; set; }
}
