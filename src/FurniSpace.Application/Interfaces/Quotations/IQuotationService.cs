using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Quotations;

namespace FurniSpace.Application.Interfaces.Quotations;

public interface IQuotationService
{
    Task<ServiceResult<QuotationListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        QuotationQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> GetDetailAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> CreateDraftAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> UpdateAsync(
        Guid quotationId,
        Guid currentUserId,
        UpdateQuotationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> AddManualItemAsync(
        Guid quotationId,
        Guid currentUserId,
        CreateManualQuotationItemRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> UpdateManualItemAsync(
        Guid quotationId,
        Guid quotationItemId,
        Guid currentUserId,
        UpdateManualQuotationItemRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> SendAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> DeleteManualItemAsync(
        Guid quotationId,
        Guid quotationItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> AcceptAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> RequestRevisionAsync(
        Guid quotationId,
        Guid currentUserId,
        RequestQuotationRevisionDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> ReviseAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> CancelAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<QuotationDetailDto>> RejectAsync(
        Guid quotationId,
        Guid currentUserId,
        RejectQuotationRequestDto request,
        CancellationToken cancellationToken = default);
}
