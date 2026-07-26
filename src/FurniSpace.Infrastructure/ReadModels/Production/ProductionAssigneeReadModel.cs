#nullable enable

using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.Production;

public sealed class ProductionAssigneeReadModel
{
    public Guid AccountId { get; set; }
    public string? RoleName { get; set; }
    public AccountStatus? Status { get; set; }
    public DateTime? DeletedAt { get; set; }
}
