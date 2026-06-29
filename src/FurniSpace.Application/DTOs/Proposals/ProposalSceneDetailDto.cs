using FurniSpace.Domain.Enums;
using FurniSpace.Shared.DTOs.Proposals;

namespace FurniSpace.Application.DTOs.Proposals;

public sealed class ProposalSceneDetailDto : ProposalSceneBaseDto<ProposalSceneType?>
{
    public Guid ProjectId { get; set; }
}
