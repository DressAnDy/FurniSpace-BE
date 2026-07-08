using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class CustomizationRequestMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProductionCustomizationRequestQueueReadModel, ProductionCustomizationRequestQueueItemDto>()
            .Ignore(dest => dest.Project)
            .Ignore(dest => dest.Proposal)
            .Ignore(dest => dest.ProposalItem);
    }
}
