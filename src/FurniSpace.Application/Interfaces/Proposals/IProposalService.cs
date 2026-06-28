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

    Task<ServiceResult<SyncProposalItemsFromSceneResponseDto>> SyncItemsFromSceneAsync(
        Guid proposalId,
        Guid currentUserId,
        SyncProposalItemsFromSceneRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SelectFinalProposalResponseDto>> SelectFinalAsync(
        Guid proposalId,
        Guid currentUserId,
        SelectFinalProposalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RequestProposalRevisionResponseDto>> RequestRevisionAsync(
        Guid proposalId,
        Guid currentUserId,
        RequestProposalRevisionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PublishProposalResponseDto>> PublishAsync(
        Guid proposalId,
        Guid currentUserId,
        PublishProposalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UpdateProposalResponseDto>> UpdateAsync(
        Guid proposalId,
        Guid currentUserId,
        UpdateProposalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UpdateProposalSceneResponseDto>> UpdateSceneAsync(
        Guid sceneId,
        Guid currentUserId,
        UpdateProposalSceneRequestDto request,
        CancellationToken cancellationToken = default);
}
