using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.DTOs.Proposals;

public sealed class ProposalListQueryReadModel
{
    public Guid ProjectId { get; set; }
    public ProposalStatus? Status { get; set; }
    public bool CustomerVisibleOnly { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
