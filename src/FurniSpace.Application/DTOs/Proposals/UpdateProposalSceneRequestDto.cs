namespace FurniSpace.Application.DTOs.Proposals;

public sealed class UpdateProposalSceneRequestDto
{
    public string? SceneName { get; set; }
    public Guid? ProjectAreaId { get; set; }
    public Guid? PreviewFileId { get; set; }
    public bool? IsActive { get; set; }
}
