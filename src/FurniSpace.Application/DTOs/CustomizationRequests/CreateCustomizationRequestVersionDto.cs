#nullable enable

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class CreateCustomizationRequestVersionDto : CustomizationRequestVersionMutationDto
{
    public IReadOnlyList<Guid> PreviewFileIds { get; set; } = [];
}
