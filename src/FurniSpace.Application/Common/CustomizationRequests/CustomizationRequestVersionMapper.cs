using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Domain.Entities;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Mapster;

namespace FurniSpace.Application.Common.CustomizationRequests;

internal static class CustomizationRequestVersionMapper
{
    public static CustomizationRequestVersionDto ToDto(
        CustomizationRequestVersion version,
        ProductVersion productVersion)
    {
        var dto = version.Adapt<CustomizationRequestVersionDto>();
        dto.ProductVersion = CustomizationAcceptedProductVersionFactory.ToProductVersionDto(productVersion);
        return dto;
    }

    public static CustomizationRequestVersionDto ToDto(CustomizationRequestVersionReadModel version)
    {
        var dto = version.Adapt<CustomizationRequestVersionDto>();
        dto.ProductVersion = CustomizationAcceptedProductVersionFactory.ToProductVersionDto(version.ProductVersion);
        return dto;
    }
}
