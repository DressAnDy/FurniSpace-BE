using System.Collections.Concurrent;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
using FurniSpace.Application.Common.Orders;
using FurniSpace.Application.Common.Quotations;
using FurniSpace.Application.Constants.Financial;
using static FurniSpace.Application.Constants.Quotations.QuotationServiceConstants;
using FurniSpace.Application.DTOs.CustomizationRequests;
using FurniSpace.Application.DTOs.Quotations;
using FurniSpace.Application.Interfaces.Notifications;
using FurniSpace.Application.Interfaces.Quotations;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.Quotations;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Mapster;
using Microsoft.Extensions.Logging;

namespace FurniSpace.Application.Services.Quotations;

public sealed class QuotationService : IQuotationService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> QuotationMutationLocks = new();

    private readonly IQuotationRepository _quotations;
    private readonly IProjectRepository _projects;
    private readonly IOrderRepository _orders;
    private readonly ICustomizationRequestRepository _customizationRequests;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrderWorkflowSettings _orderWorkflowSettings;
    //private readonly QuotationRecalculationService _recalculationService;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<QuotationService>? _logger;

    public QuotationService(
        IQuotationRepository quotations,
        IProjectRepository projects,
        IOrderRepository orders,
        ICustomizationRequestRepository customizationRequests,
        QuotationServiceDependencies dependencies)
    {
        _quotations = quotations;
        _projects = projects;
        _orders = orders;
        _customizationRequests = customizationRequests;
        _unitOfWork = dependencies.UnitOfWork;
        _orderWorkflowSettings = dependencies.OrderWorkflowSettings;
        //_recalculationService = dependencies.RecalculationService;
        _notifications = dependencies.Notifications;
        _logger = dependencies.Logger;
    }

    public async Task<ServiceResult<QuotationListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        QuotationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<QuotationListResponseDto>.Unauthorized();
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFoundList(QuotationErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanAccessProjectAssignment(
                role,
                project.CustomerId,
                project.AssignedSalesId,
                project.AssignedDesignerId,
                currentUserId))
        {
            return ServiceResult<QuotationListResponseDto>.Forbidden("You do not have access to this project's quotations.");
        }

        var readQuery = query.Adapt<QuotationQueryReadModel>();
        readQuery.ProjectId = projectId;
        var items = await _quotations.GetByProjectAsync(readQuery, cancellationToken);

        return ServiceResult<QuotationListResponseDto>.Success(
            new QuotationListResponseDto
            {
                Items = FilterByRole(items, role)
                    .Select(ToQuotationDto)
                    .ToList()
            },
            "Quotations retrieved successfully.");
    }

    public async Task<ServiceResult<QuotationDetailDto>> GetDetailAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<QuotationDetailDto>.Unauthorized();
        }

        var quotation = await _quotations.GetDetailAsync(quotationId, cancellationToken);
        if (quotation is null)
        {
            return NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanAccessProjectAssignment(
                role,
                quotation.CustomerId,
                quotation.AssignedSalesId,
                quotation.AssignedDesignerId,
                currentUserId))
        {
            return ServiceResult<QuotationDetailDto>.Forbidden("You do not have access to this quotation.");
        }

        await ExpireIfNeededAsync(quotation, cancellationToken);

        if (role == ProjectAssignmentAccessEvaluator.CustomerRole && !IsCustomerVisible(quotation.Status))
        {
            return ServiceResult<QuotationDetailDto>.Failure(Error.Forbidden(
                QuotationErrorCodes.QuotationNotAvailable,
                "Quotation is not available."));
        }

        return ServiceResult<QuotationDetailDto>.Success(
            ToDetailDto(quotation),
            "Quotation detail retrieved successfully.");
    }

    public async Task<ServiceResult<QuotationDetailDto>> CreateDraftAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return ServiceResult<QuotationDetailDto>.Unauthorized();
        }

        var project = await _projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(QuotationErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanManageAsAssignedSales(role, project.AssignedSalesId, currentUserId))
        {
            return ServiceResult<QuotationDetailDto>.Forbidden("You do not have permission to create this quotation.");
        }

        if (project.Status != ProjectStatus.PROPOSAL_SELECTED)
        {
            return BadRequestDetail(
                QuotationErrorCodes.ProjectNotReadyForQuotation,
                "Project is not ready for quotation.");
        }

        var selected = await _quotations.GetSelectedProposalAsync(projectId, cancellationToken);
        if (selected is null)
        {
            return BadRequestDetail(
                QuotationErrorCodes.ProposalNotSelected,
                "Proposal has not been selected.");
        }

        var stateError = await ValidateCreateStateAsync(selected, cancellationToken);
        if (stateError is not null)
        {
            return stateError;
        }

        var proposalItems = await _quotations.GetProposalItemsAsync(selected.ProposalId, cancellationToken);
        var quotation = CreateDraftQuotation(selected, currentUserId);
        var quotationItems = proposalItems
            .Select(item => ToQuotationItem(quotation.QuotationId, item))
            .ToList();

        QuotationRecalculationService.Recalculate(quotation, quotationItems);
        ApplyInitialDeposit(quotation, _orderWorkflowSettings.DepositPercent);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _quotations.AddAsync(quotation, cancellationToken);
            foreach (var item in quotationItems)
            {
                await _quotations.AddItemAsync(item, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var detail = await _quotations.GetDetailAsync(quotation.QuotationId, cancellationToken);
        return ServiceResult<QuotationDetailDto>.Created(
            detail is null ? ToDetailDto(quotation) : ToDetailDto(detail),
            "Draft quotation created successfully.");
    }

    public async Task<ServiceResult<QuotationDetailDto>> CreateDraftFromProposalSelectionAsync(
        Guid projectId,
        Guid proposalId,
        Guid triggeredByUserId,
        CancellationToken cancellationToken = default)
    {
        var selected = await _quotations.GetSelectedProposalAsync(projectId, cancellationToken);
        if (selected is null || selected.ProposalId != proposalId)
        {
            return BadRequestDetail(
                QuotationErrorCodes.ProposalNotSelected,
                "Proposal has not been selected.");
        }

        var stateError = await ValidateCreateStateAsync(selected, cancellationToken);
        if (stateError is not null)
        {
            return stateError;
        }

        return await AddDraftQuotationForSelectedProposalAsync(
            projectId,
            proposalId,
            triggeredByUserId,
            cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> AddDraftQuotationForSelectedProposalAsync(
        Guid projectId,
        Guid proposalId,
        Guid triggeredByUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || proposalId == Guid.Empty || triggeredByUserId == Guid.Empty)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Proposal selection context is invalid.");
        }

        if (await _quotations.HasQuotationForProposalAsync(proposalId, cancellationToken))
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationAlreadyExists,
                "Quotation already exists for this proposal.");
        }

        if (await _customizationRequests.HasPendingForProposalAsync(proposalId, cancellationToken))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationRequestPending,
                "Proposal has unresolved customization requests.");
        }

        var proposalItems = await _quotations.GetProposalItemsAsync(proposalId, cancellationToken);
        var quotation = CreateDraftQuotation(
            new SelectedProposalForQuotationReadModel
            {
                ProjectId = projectId,
                ProposalId = proposalId
            },
            triggeredByUserId);
        var quotationItems = proposalItems
            .Select(item => ToQuotationItem(quotation.QuotationId, item))
            .ToList();

        QuotationRecalculationService.Recalculate(quotation, quotationItems);
        ApplyInitialDeposit(quotation, _orderWorkflowSettings.DepositPercent);

        await _quotations.AddAsync(quotation, cancellationToken);
        foreach (var item in quotationItems)
        {
            await _quotations.AddItemAsync(item, cancellationToken);
        }

        return ServiceResult<QuotationDetailDto>.Success(
            ToDetailDto(quotation),
            "Draft quotation created successfully.");
    }

    public async Task<ServiceResult<QuotationDetailDto>> UpdateAsync(
        Guid quotationId,
        Guid currentUserId,
        UpdateQuotationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (!IsHeaderEditable(context.Detail!.Status))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation cannot be updated in its current status.");
        }

        var quotation = context.Quotation!;
        quotation.ValidUntil = request.ValidUntil;
        quotation.CustomerNote = request.CustomerNote?.Trim();
        quotation.SalesNote = request.SalesNote?.Trim();
        quotation.RevisionReason = request.RevisionReason?.Trim();
        quotation.UpdatedAt = DateTime.UtcNow;

        if (request.DepositAmount.HasValue)
        {
            await RecalculateQuotationTotalsAsync(quotation, cancellationToken);
            var depositValidation = ValidateDepositAmount(
                request.DepositAmount.Value,
                quotation.TotalAmount ?? 0m,
                requirePositive: false);
            if (depositValidation is not null)
            {
                return depositValidation;
            }

            quotation.DepositAmount = request.DepositAmount.Value;
        }
        else
        {
            await RecalculateQuotationTotalsAsync(quotation, cancellationToken);
        }

        _quotations.Update(quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation updated successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> UpdateItemFinancialsAsync(
        Guid quotationId,
        Guid quotationItemId,
        Guid currentUserId,
        UpdateQuotationItemFinancialsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetEditableItemMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        var item = await _quotations.GetItemAsync(quotationItemId, cancellationToken);
        if (item is null || item.QuotationId != quotationId)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationItemNotFound,
                QuotationItemNotFoundMessage);
        }

        var financialInput = ResolveFinancialInput(
            item,
            request.Quantity,
            request.UnitPrice,
            request.DiscountAmount);
        var validation = ValidateQuotationItem(item.ItemName, financialInput);
        if (validation is not null)
        {
            return validation;
        }

        ApplyFinancialInput(item, financialInput);
        _quotations.UpdateItem(item);

        await RecalculateQuotationTotalsAsync(context.Quotation!, cancellationToken);
        context.Quotation!.UpdatedAt = DateTime.UtcNow;
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation item financials updated successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> BulkUpdateItemFinancialsAsync(
        Guid quotationId,
        Guid currentUserId,
        BulkUpdateQuotationItemFinancialsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetEditableItemMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (request.Items is null || request.Items.Count == 0 || HasDuplicateQuotationItems(request.Items))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationItem,
                "Quotation item financial update request is invalid.");
        }

        var updates = new List<QuotationItemFinancialUpdate>();
        foreach (var requestedItem in request.Items)
        {
            var item = await _quotations.GetItemAsync(requestedItem.QuotationItemId, cancellationToken);
            if (item is null || item.QuotationId != quotationId)
            {
                return BadRequestDetail(
                    QuotationErrorCodes.QuotationItemNotFound,
                    QuotationItemNotFoundMessage);
            }

            var financialInput = ResolveFinancialInput(
                item,
                requestedItem.Quantity,
                requestedItem.UnitPrice,
                requestedItem.DiscountAmount);
            var validation = ValidateQuotationItem(item.ItemName, financialInput);
            if (validation is not null)
            {
                return validation;
            }

            updates.Add(new QuotationItemFinancialUpdate(item, financialInput));
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            ApplyFinancialUpdates(updates);
            foreach (var update in updates)
            {
                _quotations.UpdateItem(update.Item);
            }

            await RecalculateQuotationTotalsAsync(context.Quotation!, cancellationToken);
            context.Quotation!.UpdatedAt = DateTime.UtcNow;
            _quotations.Update(context.Quotation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return await LoadDetailResultAsync(quotationId, "Quotation item financials updated successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> SendAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        var quotation = context.Quotation!;
        var detail = context.Detail!;
        var items = (await _quotations.GetItemsByQuotationAsync(quotationId, cancellationToken)).ToList();
        var validation = ValidateSendState(quotation, items);
        if (validation is not null)
        {
            return validation;
        }

        QuotationRecalculationService.Recalculate(quotation, items);
        if (quotation.TotalAmount is <= 0m)
        {
            return QuotationNotReadyToSendResult();
        }

        var depositValidation = ValidateDepositAmount(
            quotation.DepositAmount ?? 0m,
            quotation.TotalAmount ?? 0m,
            requirePositive: true);
        if (depositValidation is not null)
        {
            return depositValidation;
        }

        if (quotation.ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationExpired,
                "Quotation has expired.");
        }

        var project = await _projects.GetByIdAsync(quotation.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(QuotationErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        quotation.Status = QuotationStatus.SENT;
        quotation.SentAt = now;
        quotation.UpdatedAt = now;
        project.Status = ProjectStatus.QUOTATION_SENT;
        project.UpdatedAt = now;
        foreach (var item in items)
        {
            _quotations.UpdateItem(item);
        }

        _quotations.Update(quotation);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchQuotationSentNotificationAsync(detail, cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation sent successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> AcceptAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetCustomerContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (context.Detail is null || context.Quotation is null)
        {
            return NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage);
        }

        var detail = context.Detail;
        var quotation = context.Quotation;

        if (await _orders.ExistsForQuotationAsync(quotation.QuotationId, cancellationToken))
        {
            return await LoadDetailResultAsync(quotationId, "Quotation accepted successfully.", cancellationToken);
        }

        await ExpireIfNeededAsync(detail, cancellationToken);
        var validation = ValidateAcceptState(detail);
        if (validation is not null)
        {
            return validation;
        }

        if (await _customizationRequests.HasPendingForProposalAsync(detail.ProposalId, cancellationToken))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationRequestPending,
                "Proposal has unresolved customization requests.");
        }

        var project = await _projects.GetByIdAsync(quotation.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(QuotationErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        var quotationItems = (await _quotations.GetItemsByQuotationAsync(quotationId, cancellationToken)).ToList();
        var itemValidation = ValidateAcceptItems(quotationItems);
        if (itemValidation is not null)
        {
            return itemValidation;
        }

        QuotationRecalculationService.Recalculate(quotation, quotationItems);
        if (quotation.TotalAmount is <= 0m)
        {
            return QuotationNotReadyToSendResult();
        }

        var depositValidation = ValidateDepositAmount(
            quotation.DepositAmount ?? 0m,
            quotation.TotalAmount ?? 0m,
            requirePositive: true);
        if (depositValidation is not null)
        {
            return depositValidation;
        }

        var order = CreateOrder(detail, quotation, currentUserId, now);
        var orderItems = quotationItems
            .Select(item => CreateOrderItem(order.OrderId, item))
            .ToList();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            context.Quotation.Status = QuotationStatus.ACCEPTED;
            context.Quotation.AcceptedAt = now;
            context.Quotation.UpdatedAt = now;
            project.Status = ProjectStatus.ORDER_CONFIRMED;
            project.UpdatedAt = now;

            _quotations.Update(context.Quotation);
            _projects.Update(project);
            foreach (var item in quotationItems)
            {
                _quotations.UpdateItem(item);
            }

            await _orders.AddAsync(order, cancellationToken);
            foreach (var item in orderItems)
            {
                await _orders.AddItemAsync(item, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            if (await _orders.ExistsForQuotationAsync(quotationId, cancellationToken))
            {
                return await LoadDetailResultAsync(quotationId, "Quotation accepted successfully.", cancellationToken);
            }

            throw;
        }

        await DispatchQuotationAcceptedNotificationAsync(context.Detail, cancellationToken);
        return await LoadDetailResultAsync(quotationId, "Quotation accepted successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> RequestRevisionAsync(
        Guid quotationId,
        Guid currentUserId,
        RequestQuotationRevisionDto request,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetCustomerContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        await ExpireIfNeededAsync(context.Detail!, cancellationToken);
        var revisionReason = request.RevisionReason?.Trim();
        var validation = ValidateRevisionRequestState(context.Detail!, revisionReason);
        if (validation is not null)
        {
            return validation;
        }

        var project = await _projects.GetByIdAsync(context.Quotation!.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(QuotationErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        context.Quotation.Status = QuotationStatus.REVISION_REQUESTED;
        context.Quotation.RevisionReason = revisionReason;
        context.Quotation.UpdatedAt = now;
        project.Status = ProjectStatus.QUOTATION_REVISION_REQUESTED;
        project.UpdatedAt = now;

        _quotations.Update(context.Quotation);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchQuotationRevisionRequestedNotificationAsync(context.Detail!, revisionReason!, cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation revision requested successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> ReviseAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (context.Detail!.Status != QuotationStatus.REVISION_REQUESTED)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Only revision-requested quotations can be revised.");
        }

        context.Quotation!.VersionNo = (context.Quotation.VersionNo ?? 0) + 1;
        context.Quotation.Status = QuotationStatus.REVISED;
        context.Quotation.UpdatedAt = DateTime.UtcNow;
        await RecalculateQuotationTotalsAsync(context.Quotation, cancellationToken);
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchQuotationRevisedNotificationAsync(context.Detail!, cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation revised successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> CancelAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (!CanCancelQuotation(context.Detail!.Status))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation cannot be cancelled in its current status.");
        }

        context.Quotation!.Status = QuotationStatus.CANCELLED;
        context.Quotation.UpdatedAt = DateTime.UtcNow;
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation cancelled successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> RejectAsync(
        Guid quotationId,
        Guid currentUserId,
        RejectQuotationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await AcquireQuotationMutationLockAsync(quotationId, cancellationToken);
        var context = await GetCustomerContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        await ExpireIfNeededAsync(context.Detail!, cancellationToken);
        var rejectReason = request.RejectReason?.Trim();
        var validation = ValidateRejectState(context.Detail!, rejectReason);
        if (validation is not null)
        {
            return validation;
        }

        var project = await _projects.GetByIdAsync(context.Quotation!.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFoundDetail(QuotationErrorCodes.ProjectNotFound, ProjectNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        context.Quotation.Status = QuotationStatus.REJECTED;
        context.Quotation.RejectReason = rejectReason;
        context.Quotation.RejectedAt = now;
        context.Quotation.UpdatedAt = now;
        project.Status = ProjectStatus.PROPOSAL_SELECTED;
        project.UpdatedAt = now;

        _quotations.Update(context.Quotation);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchQuotationRejectedNotificationAsync(context.Detail!, rejectReason!, cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation rejected successfully.", cancellationToken);
    }

    private static void ApplyInitialDeposit(Quotation quotation, int depositPercent)
    {
        quotation.DepositAmount = QuotationDepositCalculator.CalculateDefaultDepositAmount(
            quotation.TotalAmount ?? 0m,
            depositPercent);
    }

    private static ServiceResult<QuotationDetailDto>? ValidateDepositAmount(
        decimal depositAmount,
        decimal totalAmount,
        bool requirePositive)
    {
        if (depositAmount < 0m)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidDepositAmount,
                "Deposit amount cannot be negative.");
        }

        if (requirePositive && depositAmount <= 0m)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidDepositAmount,
                "Deposit amount must be greater than zero.");
        }

        if (depositAmount > totalAmount)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidDepositAmount,
                "Deposit amount cannot exceed total amount.");
        }

        return null;
    }

    private async Task<ServiceResult<QuotationDetailDto>?> ValidateCreateStateAsync(
        SelectedProposalForQuotationReadModel selected,
        CancellationToken cancellationToken)
    {
        if (selected.ProjectStatus != ProjectStatus.PROPOSAL_SELECTED)
        {
            return BadRequestDetail(
                QuotationErrorCodes.ProjectNotReadyForQuotation,
                "Project is not ready for quotation.");
        }

        if (selected.ProposalStatus != ProposalStatus.SELECTED)
        {
            return BadRequestDetail(
                QuotationErrorCodes.ProposalNotSelected,
                "Proposal has not been selected.");
        }

        if (await _customizationRequests.HasPendingForProposalAsync(selected.ProposalId, cancellationToken))
        {
            return BadRequestDetail(
                CustomizationRequestErrorCodes.CustomizationRequestPending,
                "Proposal has unresolved customization requests.");
        }

        return await _quotations.HasQuotationForProposalAsync(selected.ProposalId, cancellationToken)
            ? BadRequestDetail(
                QuotationErrorCodes.QuotationAlreadyExists,
                "Quotation already exists for this proposal.")
            : null;
    }

    private static Quotation CreateDraftQuotation(
        SelectedProposalForQuotationReadModel selected,
        Guid currentUserId)
    {
        var now = DateTime.UtcNow;
        return new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = selected.ProjectId,
            ProposalId = selected.ProposalId,
            QuotationCode = $"QTN-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30],
            VersionNo = 1,
            SubtotalAmount = 0m,
            TotalDiscountAmount = 0m,
            PreVatAmount = 0m,
            VatRate = FinancialConstants.DefaultVatRate,
            VatAmount = 0m,
            TotalAmount = 0m,
            Currency = "VND",
            Status = QuotationStatus.DRAFT,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Order CreateOrder(
        QuotationDetailReadModel quotation,
        Quotation quotationEntity,
        Guid currentUserId,
        DateTime now)
    {
        var total = quotationEntity.TotalAmount ?? 0m;
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = quotation.ProjectId,
            ProposalId = quotation.ProposalId,
            QuotationId = quotation.QuotationId,
            OrderCode = $"ORD-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30],
            CustomerId = quotation.CustomerId,
            SalesId = quotation.AssignedSalesId,
            VatRate = quotationEntity.VatRate ?? FinancialConstants.DefaultVatRate,
            VatAmount = quotationEntity.VatAmount ?? 0m,
            OriginalTotalAmount = total,
            ItemAdjustmentAmount = 0m,
            AdditionalDiscountAmount = 0m,
            FinalTotalAmount = total,
            DepositAmount = quotationEntity.DepositAmount ?? 0m,
            PaidAmount = 0m,
            RemainingAmount = total,
            Status = OrderStatus.CREATED,
            ConfirmedBy = currentUserId,
            ConfirmedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static OrderItem CreateOrderItem(
        Guid orderId,
        QuotationItem quotationItem)
    {
        return new OrderItem
        {
            OrderItemId = Guid.NewGuid(),
            OrderId = orderId,
            QuotationItemId = quotationItem.QuotationItemId,
            ProductVersionId = quotationItem.ProductVersionId,
            ProductNameSnapshot = quotationItem.ProductNameSnapshot ?? quotationItem.ItemName,
            ProductVersionNameSnapshot = quotationItem.ProductVersionNameSnapshot,
            ProductVersionCodeSnapshot = quotationItem.ProductVersionCodeSnapshot,
            Quantity = quotationItem.Quantity,
            DeliveredQuantity = 0,
            Status = OrderItemStatus.PENDING,
            UnitPrice = quotationItem.UnitPrice,
            DiscountAmount = quotationItem.DiscountAmount,
            SubtotalAmount = quotationItem.TotalAmount
        };
    }

    private static QuotationItem ToQuotationItem(
        Guid quotationId,
        ProposalItem item)
    {
        var now = DateTime.UtcNow;
        return new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotationId,
            ProposalItemId = item.ProposalItemId,
            ProductVersionId = item.ProductVersionId,
            ProductNameSnapshot = item.ItemName,
            ProductVersionNameSnapshot = item.ItemName,
            ItemName = item.ItemName,
            DisplayOrder = 0,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPriceSnapshot,
            DiscountAmount = 0m,
            IsCustomized = item.IsCustomized,
            CustomizationNote = item.Note,
            Note = item.Note,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static ServiceResult<QuotationDetailDto>? ValidateSendState(
        Quotation quotation,
        List<QuotationItem> items)
    {
        if (quotation.Status is not (QuotationStatus.DRAFT or QuotationStatus.REVISED) ||
            quotation.ValidUntil is null ||
            quotation.ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow) ||
            items.Count == 0)
        {
            return QuotationNotReadyToSendResult();
        }

        return items.Exists(IsInvalidSendItem)
            ? QuotationNotReadyToSendResult()
            : null;
    }

    private static bool IsInvalidSendItem(QuotationItem item)
    {
        return !item.ProposalItemId.HasValue ||
            !item.ProductVersionId.HasValue ||
            ValidateQuotationItem(
                item.ItemName,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount) is not null;
    }

    private static ServiceResult<QuotationDetailDto> QuotationNotReadyToSendResult()
    {
        return BadRequestDetail(
            QuotationErrorCodes.QuotationNotReadyToSend,
            "Quotation is not ready to send.");
    }

    private static ServiceResult<QuotationDetailDto>? ValidateAcceptState(QuotationDetailReadModel quotation)
    {
        if (quotation.Status == QuotationStatus.EXPIRED ||
            quotation.ValidUntil is null ||
            quotation.ValidUntil < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationExpired,
                "Quotation has expired.");
        }

        if (quotation.Status is not (QuotationStatus.SENT or QuotationStatus.REVISED))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation cannot be accepted in its current status.");
        }

        return null;
    }

    private static ServiceResult<QuotationDetailDto>? ValidateAcceptItems(List<QuotationItem> items)
    {
        return items.Count == 0 || items.Exists(IsInvalidSendItem)
            ? QuotationNotReadyToSendResult()
            : null;
    }

    private static ServiceResult<QuotationDetailDto>? ValidateRevisionRequestState(
        QuotationDetailReadModel quotation,
        string? revisionReason)
    {
        return ValidateCustomerDecisionState(
            quotation,
            revisionReason,
            "Quotation cannot be revision-requested in its current status.",
            QuotationErrorCodes.InvalidQuotationRevisionReason,
            "Revision reason is required.");
    }

    private static ServiceResult<QuotationDetailDto>? ValidateRejectState(
        QuotationDetailReadModel quotation,
        string? rejectReason)
    {
        return ValidateCustomerDecisionState(
            quotation,
            rejectReason,
            "Quotation cannot be rejected in its current status.",
            QuotationErrorCodes.InvalidQuotationRejectReason,
            "Reject reason is required.");
    }

    private static ServiceResult<QuotationDetailDto>? ValidateCustomerDecisionState(
        QuotationDetailReadModel quotation,
        string? reason,
        string invalidStatusMessage,
        string invalidReasonCode,
        string invalidReasonMessage)
    {
        if (quotation.Status == QuotationStatus.EXPIRED)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationExpired,
                "Quotation has expired.");
        }

        if (quotation.Status is not (QuotationStatus.SENT or QuotationStatus.REVISED))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                invalidStatusMessage);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequestDetail(
                invalidReasonCode,
                invalidReasonMessage);
        }

        return null;
    }

    private async Task DispatchQuotationRevisedNotificationAsync(
        QuotationDetailReadModel quotation,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.QuotationRevised,
                new Dictionary<string, string>
                {
                    [QuotationCodeParameter] = quotation.QuotationCode
                },
                [quotation.CustomerId],
                projectId: quotation.ProjectId,
                referenceType: QuotationReferenceType,
                referenceId: quotation.QuotationId,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch quotation revised notification for quotation {QuotationId}",
                quotation.QuotationId);
        }
    }

    private async Task DispatchQuotationSentNotificationAsync(
        QuotationDetailReadModel quotation,
        CancellationToken cancellationToken)
    {
        if (_notifications is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                NotificationType.QuotationSent,
                new Dictionary<string, string>
                {
                    [QuotationCodeParameter] = quotation.QuotationCode
                },
                [quotation.CustomerId],
                projectId: quotation.ProjectId,
                referenceType: QuotationReferenceType,
                referenceId: quotation.QuotationId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch quotation sent notification for quotation {QuotationId}",
                quotation.QuotationId);
        }
    }

    private async Task DispatchQuotationAcceptedNotificationAsync(
        QuotationDetailReadModel quotation,
        CancellationToken cancellationToken)
    {
        await DispatchQuotationStaffNotificationAsync(
            quotation,
            NotificationType.QuotationAccepted,
            new Dictionary<string, string>
            {
                [QuotationCodeParameter] = quotation.QuotationCode
            },
            "accepted",
            cancellationToken);
    }

    private async Task DispatchQuotationRevisionRequestedNotificationAsync(
        QuotationDetailReadModel quotation,
        string revisionReason,
        CancellationToken cancellationToken)
    {
        await DispatchQuotationStaffNotificationAsync(
            quotation,
            NotificationType.QuotationRevisionRequested,
            new Dictionary<string, string>
            {
                [QuotationCodeParameter] = quotation.QuotationCode,
                ["RevisionReason"] = revisionReason
            },
            "revision requested",
            cancellationToken);
    }

    private async Task DispatchQuotationRejectedNotificationAsync(
        QuotationDetailReadModel quotation,
        string rejectReason,
        CancellationToken cancellationToken)
    {
        await DispatchQuotationStaffNotificationAsync(
            quotation,
            NotificationType.QuotationRejected,
            new Dictionary<string, string>
            {
                [QuotationCodeParameter] = quotation.QuotationCode,
                ["RejectReason"] = rejectReason
            },
            "rejected",
            cancellationToken);
    }

    private async Task DispatchQuotationStaffNotificationAsync(
        QuotationDetailReadModel quotation,
        NotificationType notificationType,
        IReadOnlyDictionary<string, string> parameters,
        string actionName,
        CancellationToken cancellationToken)
    {
        if (_notifications is null || quotation.AssignedSalesId is null)
        {
            return;
        }

        try
        {
            await _notifications.DispatchAsync(
                notificationType,
                parameters,
                [quotation.AssignedSalesId.Value],
                projectId: quotation.ProjectId,
                referenceType: QuotationReferenceType,
                referenceId: quotation.QuotationId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(
                exception,
                "Failed to dispatch quotation {ActionName} notification for quotation {QuotationId}",
                actionName,
                quotation.QuotationId);
        }
    }

    private async Task<QuotationMutationContext> GetMutationContextAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return new QuotationMutationContext(ServiceResult<QuotationDetailDto>.Unauthorized());
        }

        var detail = await _quotations.GetDetailAsync(quotationId, cancellationToken);
        if (detail is null)
        {
            return new QuotationMutationContext(NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!ProjectAssignmentAccessEvaluator.CanManageAsAssignedSales(role, detail.AssignedSalesId, currentUserId))
        {
            return new QuotationMutationContext(ServiceResult<QuotationDetailDto>.Forbidden("You do not have permission to update this quotation."));
        }

        var quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken);
        return quotation is null
            ? new QuotationMutationContext(NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage))
            : new QuotationMutationContext(detail, quotation);
    }

    private async Task<QuotationMutationContext> GetEditableItemMutationContextAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context;
        }

        return IsItemEditable(context.Detail!.Status)
            ? context
            : new QuotationMutationContext(BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                QuotationItemsNotEditableMessage));
    }

    private async Task<QuotationMutationContext> GetCustomerContextAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == Guid.Empty)
        {
            return new QuotationMutationContext(ServiceResult<QuotationDetailDto>.Unauthorized());
        }

        var detail = await _quotations.GetDetailAsync(quotationId, cancellationToken);
        if (detail is null)
        {
            return new QuotationMutationContext(NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage));
        }

        var role = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (role != ProjectAssignmentAccessEvaluator.CustomerRole || detail.CustomerId != currentUserId)
        {
            return new QuotationMutationContext(ServiceResult<QuotationDetailDto>.Forbidden("You do not have permission to accept this quotation."));
        }

        var quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken);
        return quotation is null
            ? new QuotationMutationContext(NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage))
            : new QuotationMutationContext(detail, quotation);
    }

    private async Task ExpireIfNeededAsync(
        QuotationDetailReadModel quotation,
        CancellationToken cancellationToken)
    {
        if (!ShouldExpire(quotation.Status, quotation.ValidUntil))
        {
            return;
        }

        var entity = await _quotations.GetByIdAsync(quotation.QuotationId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = QuotationStatus.EXPIRED;
        entity.UpdatedAt = DateTime.UtcNow;
        quotation.Status = QuotationStatus.EXPIRED;
        quotation.UpdatedAt = entity.UpdatedAt;
        _quotations.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool ShouldExpire(
        QuotationStatus? status,
        DateOnly? validUntil)
    {
        return status is QuotationStatus.SENT or QuotationStatus.REVISED &&
            validUntil.HasValue &&
            validUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private async Task RecalculateQuotationTotalsAsync(
        Quotation quotation,
        CancellationToken cancellationToken,
        QuotationItem? unsavedItem = null,
        Guid? excludedItemId = null)
    {
        var items = await _quotations.GetItemsByQuotationAsync(quotation.QuotationId, cancellationToken);
        QuotationRecalculationService.Recalculate(quotation, items, unsavedItem, excludedItemId);
    }

    private async Task<ServiceResult<QuotationDetailDto>> LoadDetailResultAsync(
        Guid quotationId,
        string message,
        CancellationToken cancellationToken)
    {
        var detail = await _quotations.GetDetailAsync(quotationId, cancellationToken);
        return detail is null
            ? NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage)
            : ServiceResult<QuotationDetailDto>.Success(ToDetailDto(detail), message);
    }

    private static QuotationItemFinancialInput ResolveFinancialInput(
        QuotationItem item,
        int? quantity,
        decimal? unitPrice,
        decimal? discountAmount)
    {
        return new QuotationItemFinancialInput(
            quantity ?? item.Quantity,
            unitPrice ?? item.UnitPrice,
            discountAmount ?? item.DiscountAmount);
    }

    private static void ApplyFinancialUpdates(List<QuotationItemFinancialUpdate> updates)
    {
        foreach (var update in updates)
        {
            ApplyFinancialInput(update.Item, update.Input);
        }
    }

    private static void ApplyFinancialInput(
        QuotationItem item,
        QuotationItemFinancialInput input)
    {
        item.Quantity = input.Quantity;
        item.UnitPrice = input.UnitPrice;
        item.DiscountAmount = input.DiscountAmount ?? 0m;
        item.UpdatedAt = DateTime.UtcNow;
    }

    private static bool HasDuplicateQuotationItems(List<BulkUpdateQuotationItemFinancialsItemDto> items)
    {
        return items
            .GroupBy(item => item.QuotationItemId)
            .Any(group => group.Count() > 1);
    }

    private static ServiceResult<QuotationDetailDto>? ValidateQuotationItem(
        string? itemName,
        QuotationItemFinancialInput input)
    {
        return ValidateQuotationItem(
            itemName,
            input.Quantity,
            input.UnitPrice,
            input.DiscountAmount);
    }

    private static ServiceResult<QuotationDetailDto>? ValidateQuotationItem(
        string? itemName,
        int? quantity,
        decimal? unitPrice,
        decimal? discountAmount)
    {
        var grossAmount = (quantity ?? 0) * (unitPrice ?? 0m);
        if (string.IsNullOrWhiteSpace(itemName) ||
            quantity is null or <= 0 ||
            unitPrice is null or <= 0m ||
            discountAmount < 0m ||
            discountAmount > grossAmount)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationItem,
                "Quotation item is invalid.");
        }

        return null;
    }

    private static bool IsHeaderEditable(QuotationStatus? status)
    {
        return status.HasValue && HeaderEditableStatuses.Contains(status.Value);
    }

    private static bool IsItemEditable(QuotationStatus? status)
    {
        return status.HasValue && ItemEditableStatuses.Contains(status.Value);
    }

    private static bool CanCancelQuotation(QuotationStatus? status)
    {
        return status is QuotationStatus.DRAFT or QuotationStatus.REVISION_REQUESTED or QuotationStatus.REVISED;
    }

    private static async Task<QuotationMutationLock> AcquireQuotationMutationLockAsync(
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        var semaphore = QuotationMutationLocks.GetOrAdd(quotationId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new QuotationMutationLock(semaphore);
    }

    private sealed record QuotationMutationContext(
        QuotationDetailReadModel? Detail,
        Quotation? Quotation,
        ServiceResult<QuotationDetailDto>? Result)
    {
        public QuotationMutationContext(ServiceResult<QuotationDetailDto> result)
            : this(null, null, result)
        {
        }

        public QuotationMutationContext(QuotationDetailReadModel detail, Quotation quotation)
            : this(detail, quotation, null)
        {
        }
    }

    private sealed class QuotationMutationLock(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed record QuotationItemFinancialInput(
        int? Quantity,
        decimal? UnitPrice,
        decimal? DiscountAmount);

    private sealed record QuotationItemFinancialUpdate(
        QuotationItem Item,
        QuotationItemFinancialInput Input);

    private static IEnumerable<QuotationReadModel> FilterByRole(
        IEnumerable<QuotationReadModel> items,
        string? role)
    {
        return role == ProjectAssignmentAccessEvaluator.CustomerRole
            ? items.Where(item => IsCustomerVisible(item.Status))
            : items;
    }

    private static bool IsCustomerVisible(QuotationStatus? status)
    {
        return status.HasValue && CustomerVisibleStatuses.Contains(status.Value);
    }

    private static QuotationDetailDto ToDetailDto(QuotationDetailReadModel quotation)
    {
        var dto = ToQuotationDto(quotation).Adapt<QuotationDetailDto>();
        dto.Items = quotation.Items.Select(ToQuotationItemDto).ToList();
        return dto;
    }

    private static QuotationDetailDto ToDetailDto(Quotation quotation)
    {
        return ToQuotationDto(quotation).Adapt<QuotationDetailDto>();
    }

    private static QuotationDto ToQuotationDto(Quotation quotation)
    {
        return new QuotationDto
        {
            QuotationId = quotation.QuotationId,
            ProjectId = quotation.ProjectId,
            ProposalId = quotation.ProposalId,
            QuotationCode = quotation.QuotationCode,
            VersionNo = quotation.VersionNo,
            SubtotalAmount = quotation.SubtotalAmount,
            TotalDiscountAmount = quotation.TotalDiscountAmount,
            PreVatAmount = quotation.PreVatAmount,
            VatRate = quotation.VatRate,
            VatAmount = quotation.VatAmount,
            TotalAmount = quotation.TotalAmount,
            DepositAmount = quotation.DepositAmount,
            Currency = quotation.Currency,
            Status = quotation.Status,
            ValidUntil = quotation.ValidUntil,
            CustomerNote = quotation.CustomerNote,
            SalesNote = quotation.SalesNote,
            RevisionReason = quotation.RevisionReason,
            RejectReason = quotation.RejectReason,
            CreatedBy = quotation.CreatedBy,
            SentAt = quotation.SentAt,
            AcceptedAt = quotation.AcceptedAt,
            RejectedAt = quotation.RejectedAt,
            CreatedAt = quotation.CreatedAt,
            UpdatedAt = quotation.UpdatedAt
        };
    }

    private static QuotationItemDto ToQuotationItemDto(QuotationItem item)
    {
        return new QuotationItemDto
        {
            QuotationItemId = item.QuotationItemId,
            QuotationId = item.QuotationId,
            ProposalItemId = item.ProposalItemId,
            ProductVersionId = item.ProductVersionId,
            ProductNameSnapshot = item.ProductNameSnapshot,
            ProductVersionNameSnapshot = item.ProductVersionNameSnapshot,
            ProductVersionCodeSnapshot = item.ProductVersionCodeSnapshot,
            ItemName = item.ItemName,
            Description = item.Description,
            DisplayOrder = item.DisplayOrder,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            GrossAmount = item.GrossAmount,
            DiscountAmount = item.DiscountAmount,
            TotalAmount = item.TotalAmount,
            IsCustomized = item.IsCustomized,
            CustomizationNote = item.CustomizationNote,
            Note = item.Note
        };
    }

    private static ServiceResult<QuotationListResponseDto> NotFoundList(string code, string message)
    {
        return ServiceResult<QuotationListResponseDto>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<QuotationDetailDto> NotFoundDetail(string code, string message)
    {
        return ServiceResult<QuotationDetailDto>.Failure(Error.NotFound(code, message));
    }

    private static ServiceResult<QuotationDetailDto> BadRequestDetail(string code, string message)
    {
        return ServiceResult<QuotationDetailDto>.Failure(Error.BadRequest(code, message));
    }
}
