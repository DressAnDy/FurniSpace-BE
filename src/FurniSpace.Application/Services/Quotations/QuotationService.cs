using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Notifications;
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
    private const string AdminRole = "ADMIN";
    private const string CustomerRole = "CUSTOMER";
    private const string SalesRole = "SALES";
    private const string DesignerRole = "DESIGNER";
    private const string ProjectNotFoundMessage = "Project not found.";
    private const string QuotationNotFoundMessage = "Quotation not found.";
    private const string QuotationCodeParameter = "QuotationCode";
    private const string QuotationReferenceType = "QUOTATION";

    private static readonly QuotationStatus[] CustomerVisibleStatuses =
    [
        QuotationStatus.SENT,
        QuotationStatus.REVISION_REQUESTED,
        QuotationStatus.REVISED,
        QuotationStatus.ACCEPTED,
        QuotationStatus.REJECTED,
        QuotationStatus.EXPIRED
    ];

    private static readonly QuotationStatus[] HeaderEditableStatuses =
    [
        QuotationStatus.DRAFT,
        QuotationStatus.REVISION_REQUESTED,
        QuotationStatus.REVISED
    ];

    private static readonly QuotationStatus[] ManualItemEditableStatuses =
    [
        QuotationStatus.DRAFT,
        QuotationStatus.REVISED
    ];

    private readonly IQuotationRepository _quotations;
    private readonly IProjectRepository _projects;
    private readonly ICustomizationRequestRepository _customizationRequests;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher? _notifications;
    private readonly ILogger<QuotationService>? _logger;

    public QuotationService(
        IQuotationRepository quotations,
        IProjectRepository projects,
        ICustomizationRequestRepository customizationRequests,
        IUnitOfWork unitOfWork,
        INotificationDispatcher? notifications = null,
        ILogger<QuotationService>? logger = null)
    {
        _quotations = quotations;
        _projects = projects;
        _customizationRequests = customizationRequests;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _logger = logger;
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
        if (!CanAccessProject(role, project.CustomerId, project.AssignedSalesId, project.AssignedDesignerId, currentUserId))
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
                    .Select(item => item.Adapt<QuotationDto>())
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
        if (!CanAccessProject(role, quotation.CustomerId, quotation.AssignedSalesId, quotation.AssignedDesignerId, currentUserId))
        {
            return ServiceResult<QuotationDetailDto>.Forbidden("You do not have access to this quotation.");
        }

        await ExpireIfNeededAsync(quotation, cancellationToken);

        if (role == CustomerRole && !IsCustomerVisible(quotation.Status))
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
        if (!CanCreateDraft(role, project.AssignedSalesId, currentUserId))
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
        var quotation = CreateDraftQuotation(selected, currentUserId, proposalItems);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _quotations.AddAsync(quotation, cancellationToken);
            foreach (var item in proposalItems.Select(item => ToQuotationItem(quotation.QuotationId, item)))
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
            detail is null ? quotation.Adapt<QuotationDetailDto>() : ToDetailDto(detail),
            "Draft quotation created successfully.");
    }

    public async Task<ServiceResult<QuotationDetailDto>> UpdateAsync(
        Guid quotationId,
        Guid currentUserId,
        UpdateQuotationRequestDto request,
        CancellationToken cancellationToken = default)
    {
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

        var validation = ValidateMoney(request.DiscountAmount, request.TaxAmount);
        if (validation is not null)
        {
            return validation;
        }

        var quotation = context.Quotation!;
        quotation.ValidUntil = request.ValidUntil;
        quotation.DiscountAmount = request.DiscountAmount ?? 0m;
        quotation.TaxAmount = request.TaxAmount ?? 0m;
        quotation.CustomerNote = request.CustomerNote?.Trim();
        quotation.SalesNote = request.SalesNote?.Trim();
        quotation.RevisionReason = request.RevisionReason?.Trim();
        quotation.UpdatedAt = DateTime.UtcNow;

        await RecalculateQuotationTotalsAsync(quotation, cancellationToken);
        _quotations.Update(quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation updated successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> AddManualItemAsync(
        Guid quotationId,
        Guid currentUserId,
        CreateManualQuotationItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (!IsManualItemEditable(context.Detail!.Status))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation items cannot be updated in this quotation status.");
        }

        var validation = ValidateManualItem(request.ItemName, request.Quantity, request.UnitPrice, request.DiscountAmount);
        if (validation is not null)
        {
            return validation;
        }

        var item = new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotationId,
            ItemType = QuotationItemType.MANUAL_ITEM,
            ItemName = request.ItemName!.Trim(),
            Description = request.Description?.Trim(),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            CustomizationAdditionalCost = 0m,
            DiscountAmount = request.DiscountAmount ?? 0m,
            SubtotalAmount = CalculateItemSubtotal(request.Quantity, request.UnitPrice, 0m, request.DiscountAmount),
            IsCustomized = false,
            Note = request.Note?.Trim()
        };

        await RecalculateQuotationTotalsAsync(context.Quotation!, cancellationToken, item);
        context.Quotation!.UpdatedAt = DateTime.UtcNow;
        await _quotations.AddItemAsync(item, cancellationToken);
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Manual quotation item created successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> UpdateManualItemAsync(
        Guid quotationId,
        Guid quotationItemId,
        Guid currentUserId,
        UpdateManualQuotationItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (!IsManualItemEditable(context.Detail!.Status))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation items cannot be updated in this quotation status.");
        }

        var item = await _quotations.GetItemAsync(quotationItemId, cancellationToken);
        if (item is null || item.QuotationId != quotationId)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationItemNotFound,
                "Quotation item not found.");
        }

        if (item.ItemType != QuotationItemType.MANUAL_ITEM)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationItemNotEditable,
                "Product quotation items are not editable.");
        }

        var itemName = request.ItemName ?? item.ItemName;
        var quantity = request.Quantity ?? item.Quantity;
        var unitPrice = request.UnitPrice ?? item.UnitPrice;
        var discount = request.DiscountAmount ?? item.DiscountAmount;
        var validation = ValidateManualItem(itemName, quantity, unitPrice, discount);
        if (validation is not null)
        {
            return validation;
        }

        item.ItemName = itemName!.Trim();
        item.Description = request.Description?.Trim() ?? item.Description;
        item.Quantity = quantity;
        item.UnitPrice = unitPrice;
        item.CustomizationAdditionalCost = 0m;
        item.DiscountAmount = discount ?? 0m;
        item.SubtotalAmount = CalculateItemSubtotal(quantity, unitPrice, 0m, discount);
        item.Note = request.Note?.Trim() ?? item.Note;
        _quotations.UpdateItem(item);

        await RecalculateQuotationTotalsAsync(context.Quotation!, cancellationToken);
        context.Quotation!.UpdatedAt = DateTime.UtcNow;
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Manual quotation item updated successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> SendAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        var quotation = context.Quotation!;
        var detail = context.Detail!;
        var validation = ValidateSendState(detail);
        if (validation is not null)
        {
            return validation;
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
        _quotations.Update(quotation);
        _projects.Update(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await DispatchQuotationSentNotificationAsync(detail, cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation sent successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> DeleteManualItemAsync(
        Guid quotationId,
        Guid quotationItemId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetMutationContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        if (!IsManualItemEditable(context.Detail!.Status))
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation items cannot be updated in this quotation status.");
        }

        var item = await _quotations.GetItemAsync(quotationItemId, cancellationToken);
        if (item is null || item.QuotationId != quotationId)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationItemNotFound,
                "Quotation item not found.");
        }

        if (item.ItemType != QuotationItemType.MANUAL_ITEM)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationItemNotEditable,
                "Product quotation items are not deletable.");
        }

        await RecalculateQuotationTotalsAsync(context.Quotation!, cancellationToken, excludedItemId: quotationItemId);
        context.Quotation!.UpdatedAt = DateTime.UtcNow;
        _quotations.RemoveItem(item);
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Manual quotation item deleted successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> AcceptAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetCustomerContextAsync(quotationId, currentUserId, cancellationToken);
        if (context.Result is not null)
        {
            return context.Result;
        }

        await ExpireIfNeededAsync(context.Detail!, cancellationToken);
        var validation = ValidateAcceptState(context.Detail!);
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
        var order = CreateOrder(context.Detail!, currentUserId, now);
        var orderItems = context.Detail!.Items
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
            await _quotations.AddOrderAsync(order, cancellationToken);
            foreach (var item in orderItems)
            {
                await _quotations.AddOrderItemAsync(item, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
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
        _quotations.Update(context.Quotation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await LoadDetailResultAsync(quotationId, "Quotation revised successfully.", cancellationToken);
    }

    public async Task<ServiceResult<QuotationDetailDto>> CancelAsync(
        Guid quotationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
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
        Guid currentUserId,
        IReadOnlyList<ProposalItem> proposalItems)
    {
        var now = DateTime.UtcNow;
        var subtotal = proposalItems.Sum(item => item.TotalPriceSnapshot ?? 0m);
        return new Quotation
        {
            QuotationId = Guid.NewGuid(),
            ProjectId = selected.ProjectId,
            ProposalId = selected.ProposalId,
            QuotationCode = $"QTN-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30],
            VersionNo = 1,
            SubtotalAmount = subtotal,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAmount = subtotal,
            Status = QuotationStatus.DRAFT,
            CreatedBy = currentUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static Order CreateOrder(
        QuotationDetailReadModel quotation,
        Guid currentUserId,
        DateTime now)
    {
        var total = quotation.TotalAmount ?? 0m;
        return new Order
        {
            OrderId = Guid.NewGuid(),
            ProjectId = quotation.ProjectId,
            ProposalId = quotation.ProposalId,
            QuotationId = quotation.QuotationId,
            OrderCode = $"ORD-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30],
            CustomerId = quotation.CustomerId,
            SalesId = quotation.AssignedSalesId,
            OriginalTotalAmount = total,
            ItemAdjustmentAmount = 0m,
            AdditionalDiscountAmount = 0m,
            FinalTotalAmount = total,
            PaidAmount = 0m,
            RemainingAmount = total,
            Status = OrderStatus.DEPOSIT_PENDING,
            ConfirmedBy = currentUserId,
            ConfirmedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static OrderItem CreateOrderItem(
        Guid orderId,
        QuotationItemReadModel quotationItem)
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
            CustomizationFee = quotationItem.CustomizationAdditionalCost,
            DiscountAmount = quotationItem.DiscountAmount,
            SubtotalAmount = quotationItem.SubtotalAmount
        };
    }

    private static QuotationItem ToQuotationItem(Guid quotationId, ProposalItem item)
    {
        return new QuotationItem
        {
            QuotationItemId = Guid.NewGuid(),
            QuotationId = quotationId,
            ItemType = QuotationItemType.PRODUCT_ITEM,
            ProposalItemId = item.ProposalItemId,
            ProductVersionId = item.ProductVersionId,
            ProductNameSnapshot = item.ItemName,
            ProductVersionNameSnapshot = item.ItemName,
            ItemName = item.ItemName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPriceSnapshot,
            CustomizationAdditionalCost = 0m,
            DiscountAmount = 0m,
            SubtotalAmount = item.TotalPriceSnapshot,
            IsCustomized = item.IsCustomized,
            CustomizationNote = item.Note,
            Note = item.Note
        };
    }

    private static bool CanCreateDraft(string? role, Guid? assignedSalesId, Guid currentUserId)
    {
        return role == AdminRole || role == SalesRole && assignedSalesId == currentUserId;
    }

    private static ServiceResult<QuotationDetailDto>? ValidateSendState(QuotationDetailReadModel quotation)
    {
        if (quotation.Status is not (QuotationStatus.DRAFT or QuotationStatus.REVISED) ||
            quotation.ValidUntil is null ||
            quotation.TotalAmount is null or <= 0m ||
            quotation.Items.Count == 0)
        {
            return BadRequestDetail(
                QuotationErrorCodes.QuotationNotReadyToSend,
                "Quotation is not ready to send.");
        }

        return null;
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

        if (quotation.Status != QuotationStatus.SENT)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationStatus,
                "Quotation cannot be accepted in its current status.");
        }

        return null;
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

    private static bool CanManageQuotation(string? role, Guid? assignedSalesId, Guid currentUserId)
    {
        return role == AdminRole || role == SalesRole && assignedSalesId == currentUserId;
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
        if (!CanManageQuotation(role, detail.AssignedSalesId, currentUserId))
        {
            return new QuotationMutationContext(ServiceResult<QuotationDetailDto>.Forbidden("You do not have permission to update this quotation."));
        }

        var quotation = await _quotations.GetByIdAsync(quotationId, cancellationToken);
        return quotation is null
            ? new QuotationMutationContext(NotFoundDetail(QuotationErrorCodes.QuotationNotFound, QuotationNotFoundMessage))
            : new QuotationMutationContext(detail, quotation);
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
        if (role != CustomerRole || detail.CustomerId != currentUserId)
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
        var subtotal = items
            .Where(item => item.QuotationItemId != excludedItemId)
            .Sum(item => item.SubtotalAmount ?? 0m) + (unsavedItem?.SubtotalAmount ?? 0m);
        quotation.SubtotalAmount = subtotal;
        quotation.TotalAmount = subtotal - (quotation.DiscountAmount ?? 0m) + (quotation.TaxAmount ?? 0m);
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

    private static ServiceResult<QuotationDetailDto>? ValidateMoney(
        decimal? discountAmount,
        decimal? taxAmount)
    {
        return discountAmount < 0m || taxAmount < 0m
            ? BadRequestDetail(QuotationErrorCodes.InvalidQuotationItem, "Discount and tax must be greater than or equal to zero.")
            : null;
    }

    private static ServiceResult<QuotationDetailDto>? ValidateManualItem(
        string? itemName,
        int? quantity,
        decimal? unitPrice,
        decimal? discountAmount)
    {
        if (string.IsNullOrWhiteSpace(itemName) || quantity is null or <= 0 || unitPrice is null or < 0m || discountAmount < 0m)
        {
            return BadRequestDetail(
                QuotationErrorCodes.InvalidQuotationItem,
                "Manual quotation item is invalid.");
        }

        return null;
    }

    private static decimal CalculateItemSubtotal(
        int? quantity,
        decimal? unitPrice,
        decimal? customizationAdditionalCost,
        decimal? discountAmount)
    {
        return (quantity ?? 0) * ((unitPrice ?? 0m) + (customizationAdditionalCost ?? 0m)) - (discountAmount ?? 0m);
    }

    private static bool IsHeaderEditable(QuotationStatus? status)
    {
        return status.HasValue && HeaderEditableStatuses.Contains(status.Value);
    }

    private static bool IsManualItemEditable(QuotationStatus? status)
    {
        return status.HasValue && ManualItemEditableStatuses.Contains(status.Value);
    }

    private static bool CanCancelQuotation(QuotationStatus? status)
    {
        return status is QuotationStatus.DRAFT or QuotationStatus.REVISION_REQUESTED or QuotationStatus.REVISED;
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

    private static bool CanAccessProject(
        string? role,
        Guid customerId,
        Guid? assignedSalesId,
        Guid? assignedDesignerId,
        Guid currentUserId)
    {
        return role switch
        {
            AdminRole => true,
            CustomerRole => customerId == currentUserId,
            SalesRole => assignedSalesId == currentUserId,
            DesignerRole => assignedDesignerId == currentUserId,
            _ => false
        };
    }

    private static IEnumerable<QuotationReadModel> FilterByRole(
        IEnumerable<QuotationReadModel> items,
        string? role)
    {
        return role == CustomerRole
            ? items.Where(item => IsCustomerVisible(item.Status))
            : items;
    }

    private static bool IsCustomerVisible(QuotationStatus? status)
    {
        return status.HasValue && CustomerVisibleStatuses.Contains(status.Value);
    }

    private static QuotationDetailDto ToDetailDto(QuotationDetailReadModel quotation)
    {
        var dto = quotation.Adapt<QuotationDetailDto>();
        dto.Items = quotation.Items.Adapt<List<QuotationItemDto>>();
        return dto;
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
