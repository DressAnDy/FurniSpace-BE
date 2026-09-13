namespace FurniSpace.Application.DTOs.Proposals;

public sealed class UpdateProposalSceneRequestDto
{
    public string? SceneName { get; set; }
    public List<Guid>? ProjectAreaIds { get; set; }
    public Guid? PreviewFileId { get; set; }
    public bool? IsActive { get; set; }
}
