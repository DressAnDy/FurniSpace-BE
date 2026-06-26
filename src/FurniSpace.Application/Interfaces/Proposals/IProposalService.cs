using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Proposals;
using FurniSpace.Domain.Enums;

namespace FurniSpace.Application.Interfaces.Proposals;

public interface IProposalService
{
    Task<ServiceResult<ProposalDto>> CreateAsync(
        Guid projectId,
        Guid currentUserId,
        CreateProposalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProposalListResponseDto>> GetListByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        ProposalListQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProposalSceneDto>> CreateSceneAsync(
        Guid proposalId,
        Guid currentUserId,
        CreateProposalSceneRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProposalDetailDto>> GetDetailAsync(
        Guid proposalId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
