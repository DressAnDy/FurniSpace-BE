using FurniSpace.Domain.Entities;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class CustomizationRequestDetailReadModel : CustomizationRequestReadModel
{
    public ProductVersion SourceProductVersion { get; set; } = new();
}
