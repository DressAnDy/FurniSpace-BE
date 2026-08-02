#nullable enable

namespace FurniSpace.Application.DTOs.CustomizationRequests;

public sealed class UpdateCustomizationRequestVersionDto : CustomizationRequestVersionMutationDto
{
    public IReadOnlyList<Guid>? PreviewFileIds { get; set; }
}
