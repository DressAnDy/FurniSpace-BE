using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalListQueryDto
{
    public ProposalStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
