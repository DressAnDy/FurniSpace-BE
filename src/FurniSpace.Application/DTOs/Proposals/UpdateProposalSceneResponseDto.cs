using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class UpdateProposalSceneResponseDto
{
    public Guid SceneId { get; set; }
    public Guid ProposalId { get; set; }
    public string? SceneName { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public ProposalSceneType? SceneType { get; set; }
    public string? MongoSceneId { get; set; }
    public Guid? PreviewFileId { get; set; }
    public bool? IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
