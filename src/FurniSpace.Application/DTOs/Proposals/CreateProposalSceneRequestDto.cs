using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class CreateProposalSceneRequestDto
{
    public string? SceneName { get; set; }
    public ProposalSceneType? SceneType { get; set; }
    public List<Guid> ProjectAreaIds { get; set; } = [];
    public Guid? PreviewFileId { get; set; }
}
