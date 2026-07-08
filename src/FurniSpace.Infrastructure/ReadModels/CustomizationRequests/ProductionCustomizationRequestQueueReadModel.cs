using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

public sealed class ProductionCustomizationRequestQueueReadModel : CustomizationRequestReadModel
{
    public string ProposalName { get; set; } = string.Empty;

    public ProposalStatus? ProposalStatus { get; set; }

    public ProposalItem ProposalItem { get; set; } = new();
}
