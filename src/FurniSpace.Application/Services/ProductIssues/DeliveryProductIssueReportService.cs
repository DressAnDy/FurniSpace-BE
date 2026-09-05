using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.Constants.Common;
using FurniSpace.Application.Constants.ProductIssues;
using FurniSpace.Application.DTOs.ProductIssues;
using FurniSpace.Application.Interfaces.ProductIssues;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using FurniSpace.Infrastructure.Interfaces;
using FurniSpace.Infrastructure.Persistence;
using FurniSpace.Infrastructure.ReadModels.ProductIssues;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.Extensions.Options;
using static FurniSpace.Application.Constants.ProductIssues.ProductIssueServiceConstants;

namespace FurniSpace.Application.Services.ProductIssues;

public sealed class DeliveryProductIssueReportService : IDeliveryProductIssueReportService
{
    private const string OrderNotFoundMessage = "Order not found.";
    private const string IssueNotFoundMessage = "Product issue report not found.";
    private const string ForbiddenMessage = "You do not have access to product issue reports for this context.";
    private const int MaxDescriptionLength = 4000;

    private readonly IDeliveryProductIssueReportRepository _issues;
    private readonly IOrderRepository _orders;
    private readonly IProjectRepository _projects;
    private readonly IProductionRequestRepository _productionRequests;
    private readonly IDeliveryRepository _deliveries;
    private readonly IProjectFileRepository _files;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly IFileUploadValidator _fileUploadValidator;
    private readonly FirebaseStorageSettings _firebaseSettings;

    public DeliveryProductIssueReportService(
        IDeliveryProductIssueReportRepository issues,
        IOrderRepository orders,
        IProjectRepository projects,
        IProductionRequestRepository productionRequests,
        IDeliveryRepository deliveries,
        IProjectFileRepository files,
        IUnitOfWork unitOfWork,
        IFileStorageService storage,
        IFileUploadValidator fileUploadValidator,
        IOptions<FirebaseStorageSettings> firebaseSettings)
    {
        _issues = issues;
        _orders = orders;
        _projects = projects;
        _productionRequests = productionRequests;
        _deliveries = deliveries;
        _files = files;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _fileUploadValidator = fileUploadValidator;
        _firebaseSettings = firebaseSettings.Value;
    }

    public async Task<ServiceResult<ProductIssueReportDto>> CreateAsync(
        Guid orderId,
        Guid currentUserId,
        CreateProductIssueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCreateRequest(orderId, currentUserId, request);
        if (validationError is not null)
        {
            return validationError;
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName))
        {
            return Forbidden();
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(ProductIssueErrorCodes.OrderNotFound, OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null || project.CustomerId != currentUserId)
        {
            return Forbidden();
        }

        var orderItem = await _orders.GetItemByIdAsync(request.OrderItemId, cancellationToken);
        if (orderItem is null || orderItem.OrderId != orderId)
        {
            return BadRequest(
                ProductIssueErrorCodes.OrderItemOrderMismatch,
                "Order item does not belong to this order.");
        }

        if (orderItem.DeliveredQuantity <= 0)
        {
            return BadRequest(
                ProductIssueErrorCodes.NotDelivered,
                "Product issue can only be reported after at least one unit has been physically delivered.");
        }

        var quantityValidationError = ValidateAffectedQuantity(request.AffectedQuantity, orderItem.DeliveredQuantity);
        if (quantityValidationError is not null)
        {
            return quantityValidationError;
        }

        if (request.DeliveryItemId.HasValue)
        {
            var deliveryItemError = await ValidateDeliveryItemAsync(
                request.DeliveryItemId.Value,
                orderId,
                request.OrderItemId,
                cancellationToken);
            if (deliveryItemError is not null)
            {
                return deliveryItemError;
            }
        }

        var uploadedObjects = new List<string>();
        var evidenceValidationError = ValidateEvidenceFiles(request.EvidenceFiles);
        if (evidenceValidationError is not null)
        {
            return evidenceValidationError;
        }

        var now = DateTime.UtcNow;
        var issueId = Guid.NewGuid();
        var issue = new DeliveryProductIssueReport
        {
            DeliveryProductIssueReportId = issueId,
            ProjectId = order.ProjectId,
            OrderId = orderId,
            OrderItemId = request.OrderItemId,
            DeliveryItemId = request.DeliveryItemId,
            IssueType = request.IssueType,
            Description = request.Description.Trim(),
            AffectedQuantity = request.AffectedQuantity,
            ReportedBy = currentUserId,
            ReportedAt = now,
            CreatedAt = now
        };

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _issues.AddAsync(issue, cancellationToken);

            foreach (var evidence in request.EvidenceFiles)
            {
                var stored = await UploadEvidenceAsync(
                    issueId,
                    order.ProjectId,
                    currentUserId,
                    evidence,
                    uploadedObjects,
                    cancellationToken);
                await _files.AddAsync(stored.StoredFile, cancellationToken);
                await _files.AddFileLinkAsync(stored.FileLink, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            await DeleteUploadedObjectsAsync(uploadedObjects, cancellationToken);
            throw;
        }

        var detail = await _issues.GetDetailAsync(issueId, cancellationToken);
        return ServiceResult<ProductIssueReportDto>.Created(
            ToDto(detail ?? MapFallbackDetail(issue, project.ProjectName, orderItem.ProductNameSnapshot)),
            "Product issue report submitted successfully.");
    }

    public async Task<ServiceResult<ProductIssueReportListResponseDto>> GetByOrderAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequestList(ProductIssueErrorCodes.InvalidRequest, "Order id is required.");
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return ServiceResult<ProductIssueReportListResponseDto>.NotFound(OrderNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewAsync(order.ProjectId, order.CustomerId, roleName, currentUserId, cancellationToken))
        {
            return ServiceResult<ProductIssueReportListResponseDto>.Forbidden(ForbiddenMessage);
        }

        var items = await _issues.GetByOrderAsync(orderId, cancellationToken);
        return ServiceResult<ProductIssueReportListResponseDto>.Success(
            new ProductIssueReportListResponseDto { Items = items.Select(ToDto).ToList() },
            "Product issue reports retrieved successfully.");
    }

    public async Task<ServiceResult<ProductIssueReportListResponseDto>> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequestList(ProductIssueErrorCodes.InvalidRequest, "Project id is required.");
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ServiceResult<ProductIssueReportListResponseDto>.NotFound("Project not found.");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewAsync(project.ProjectId, project.CustomerId, roleName, currentUserId, cancellationToken))
        {
            return ServiceResult<ProductIssueReportListResponseDto>.Forbidden(ForbiddenMessage);
        }

        var items = await _issues.GetByProjectAsync(projectId, cancellationToken);
        return ServiceResult<ProductIssueReportListResponseDto>.Success(
            new ProductIssueReportListResponseDto { Items = items.Select(ToDto).ToList() },
            "Product issue reports retrieved successfully.");
    }

    public async Task<ServiceResult<ProductIssueReportDto>> GetDetailAsync(
        Guid issueId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (issueId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequest(ProductIssueErrorCodes.InvalidRequest, "Issue id is required.");
        }

        var detail = await _issues.GetDetailAsync(issueId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ProductIssueErrorCodes.IssueNotFound, IssueNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(detail.ProjectId, cancellationToken);
        if (project is null)
        {
            return NotFound(ProductIssueErrorCodes.IssueNotFound, IssueNotFoundMessage);
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!await CanViewAsync(project.ProjectId, project.CustomerId, roleName, currentUserId, cancellationToken))
        {
            return Forbidden();
        }

        return ServiceResult<ProductIssueReportDto>.Success(
            ToDto(detail),
            "Product issue report retrieved successfully.");
    }

    private async Task<(StoredFile StoredFile, FileLink FileLink)> UploadEvidenceAsync(
        Guid issueId,
        Guid projectId,
        Guid currentUserId,
        ProductIssueEvidenceUploadDto evidence,
        List<string> uploadedObjects,
        CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var originalFileName = Path.GetFileName(evidence.OriginalFileName.Trim());
        var generatedFileName = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            _firebaseSettings,
            projectId,
            generatedFileName);

        var uploadResult = await _storage.UploadAsync(
            new StorageUploadRequest
            {
                Content = evidence.Content,
                ObjectName = objectName,
                ContentType = ProjectFileUploadSupport.NormalizeContentType(evidence.ContentType)
            },
            cancellationToken);
        uploadedObjects.Add(uploadResult.ObjectName);

        var storedFile = new StoredFile
        {
            FileId = fileId,
            UploadedBy = currentUserId,
            OriginalFileName = originalFileName,
            StoredFileName = generatedFileName,
            FileUrl = uploadResult.PublicUrl,
            StoragePath = uploadResult.ObjectName,
            MimeType = ProjectFileUploadSupport.NormalizeContentType(evidence.ContentType),
            FileExtension = ProjectFileUploadSupport.NormalizeExtension(originalFileName),
            FileSizeBytes = evidence.FileSizeBytes,
            Status = FileStatus.ACTIVE,
            UploadedAt = now
        };

        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = IssueReportReferenceType,
            ReferenceId = issueId,
            FileType = FileType.PRODUCT_ISSUE_EVIDENCE,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        return (storedFile, fileLink);
    }

    private async Task DeleteUploadedObjectsAsync(
        IReadOnlyList<string> uploadedObjects,
        CancellationToken cancellationToken)
    {
        foreach (var objectName in uploadedObjects)
        {
            try
            {
                await _storage.DeleteAsync(objectName, cancellationToken);
            }
            catch
            {
                // Best-effort cleanup after failed transaction.
            }
        }
    }

    private async Task<ServiceResult<ProductIssueReportDto>?> ValidateDeliveryItemAsync(
        Guid deliveryItemId,
        Guid orderId,
        Guid orderItemId,
        CancellationToken cancellationToken)
    {
        var deliveryItem = await _deliveries.GetItemByIdAsync(deliveryItemId, cancellationToken);
        if (deliveryItem is null)
        {
            return BadRequest(ProductIssueErrorCodes.DeliveryItemNotFound, "Delivery item not found.");
        }

        if (deliveryItem.OrderItemId != orderItemId)
        {
            return BadRequest(
                ProductIssueErrorCodes.DeliveryItemOrderItemMismatch,
                "Delivery item does not belong to the specified order item.");
        }

        var delivery = await _deliveries.GetByIdAsync(deliveryItem.DeliveryId, cancellationToken);
        if (delivery is null || delivery.OrderId != orderId)
        {
            return BadRequest(
                ProductIssueErrorCodes.DeliveryItemOrderItemMismatch,
                "Delivery item does not belong to this order.");
        }

        return null;
    }

    private async Task<bool> CanViewAsync(
        Guid projectId,
        Guid customerId,
        string? roleName,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (IsAdmin(roleName))
        {
            return true;
        }

        if (IsCustomer(roleName))
        {
            return customerId == currentUserId;
        }

        if (IsDesigner(roleName))
        {
            return false;
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return false;
        }

        if (IsSales(roleName))
        {
            return project.AssignedSalesId == currentUserId;
        }

        if (IsProduction(roleName))
        {
            return await _productionRequests.HasViewableAssignedRequestAsync(
                projectId,
                currentUserId,
                cancellationToken);
        }

        return false;
    }

    private ServiceResult<ProductIssueReportDto>? ValidateEvidenceFiles(
        IReadOnlyList<ProductIssueEvidenceUploadDto> files)
    {
        foreach (var file in files)
        {
            var validation = _fileUploadValidator.Validate(new ProductIssueEvidenceUploadPayload(file));
            if (!validation.IsValid)
            {
                return MapFileValidationResult(validation);
            }
        }

        return null;
    }

    private static ServiceResult<ProductIssueReportDto> MapFileValidationResult(
        FileUploadValidationResult validation)
    {
        return validation.FailureKind switch
        {
            FileUploadValidationFailureKind.FileTooLarge =>
                ServiceResult<ProductIssueReportDto>.PayloadTooLarge(validation.Message),
            FileUploadValidationFailureKind.InvalidExtension or FileUploadValidationFailureKind.InvalidMimeType =>
                ServiceResult<ProductIssueReportDto>.UnsupportedMediaType(validation.Message),
            _ => BadRequest(ProductIssueErrorCodes.InvalidRequest, validation.Message)
        };
    }

    private static ServiceResult<ProductIssueReportDto>? ValidateCreateRequest(
        Guid orderId,
        Guid currentUserId,
        CreateProductIssueRequestDto request)
    {
        if (orderId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return BadRequest(ProductIssueErrorCodes.InvalidRequest, "Order id is required.");
        }

        if (request.OrderItemId == Guid.Empty)
        {
            return BadRequest(ProductIssueErrorCodes.InvalidRequest, "Order item id is required.");
        }

        if (!Enum.IsDefined(typeof(DeliveryProductIssueType), request.IssueType))
        {
            return BadRequest(ProductIssueErrorCodes.InvalidRequest, "Issue type is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(ProductIssueErrorCodes.InvalidRequest, "Description is required.");
        }

        if (request.Description.Trim().Length > MaxDescriptionLength)
        {
            return BadRequest(
                ProductIssueErrorCodes.InvalidRequest,
                $"Description must not exceed {MaxDescriptionLength} characters.");
        }

        return null;
    }

    private static ServiceResult<ProductIssueReportDto>? ValidateAffectedQuantity(
        int? affectedQuantity,
        int deliveredQuantity)
    {
        if (!affectedQuantity.HasValue)
        {
            return null;
        }

        if (affectedQuantity.Value <= 0)
        {
            return BadRequest(
                ProductIssueErrorCodes.InvalidAffectedQuantity,
                "Affected quantity must be greater than zero.");
        }

        if (affectedQuantity.Value > deliveredQuantity)
        {
            return BadRequest(
                ProductIssueErrorCodes.InvalidAffectedQuantity,
                "Affected quantity must not exceed delivered quantity.");
        }

        return null;
    }

    private static ProductIssueReportDto ToDto(DeliveryProductIssueReportListItemReadModel item)
    {
        return new ProductIssueReportDto
        {
            DeliveryProductIssueReportId = item.DeliveryProductIssueReportId,
            ProjectId = item.ProjectId,
            OrderId = item.OrderId,
            OrderItemId = item.OrderItemId,
            DeliveryItemId = item.DeliveryItemId,
            IssueType = item.IssueType.ToString(),
            Description = item.Description,
            AffectedQuantity = item.AffectedQuantity,
            ReportedBy = item.ReportedBy,
            ReporterName = item.ReporterName,
            ReportedAt = item.ReportedAt,
            CreatedAt = item.CreatedAt
        };
    }

    private static ProductIssueReportDto ToDto(DeliveryProductIssueReportDetailReadModel item)
    {
        var dto = ToDto((DeliveryProductIssueReportListItemReadModel)item);
        dto.ProjectName = item.ProjectName;
        dto.ProductNameSnapshot = item.ProductNameSnapshot;
        dto.EvidenceFiles = item.EvidenceFiles
            .Select(file => new ProductIssueEvidenceFileDto
            {
                FileId = file.FileId,
                FileLinkId = file.FileLinkId,
                OriginalFileName = file.OriginalFileName,
                FileUrl = file.FileUrl,
                MimeType = file.MimeType,
                FileSizeBytes = file.FileSizeBytes
            })
            .ToList();
        return dto;
    }

    private static DeliveryProductIssueReportDetailReadModel MapFallbackDetail(
        DeliveryProductIssueReport issue,
        string? projectName,
        string? productNameSnapshot)
    {
        return new DeliveryProductIssueReportDetailReadModel
        {
            DeliveryProductIssueReportId = issue.DeliveryProductIssueReportId,
            ProjectId = issue.ProjectId,
            ProjectName = projectName,
            OrderId = issue.OrderId,
            OrderItemId = issue.OrderItemId,
            DeliveryItemId = issue.DeliveryItemId,
            IssueType = issue.IssueType,
            Description = issue.Description,
            AffectedQuantity = issue.AffectedQuantity,
            ReportedBy = issue.ReportedBy,
            ReportedAt = issue.ReportedAt,
            CreatedAt = issue.CreatedAt,
            ProductNameSnapshot = productNameSnapshot
        };
    }

    private static bool IsAdmin(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase);

    private static bool IsSales(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Sales, StringComparison.OrdinalIgnoreCase);

    private static bool IsProduction(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Production, StringComparison.OrdinalIgnoreCase);

    private static bool IsCustomer(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Customer, StringComparison.OrdinalIgnoreCase);

    private static bool IsDesigner(string? roleName) =>
        string.Equals(roleName, ApplicationRoles.Designer, StringComparison.OrdinalIgnoreCase);

    private static ServiceResult<ProductIssueReportDto> NotFound(string code, string message) =>
        ServiceResult<ProductIssueReportDto>.Failure(Error.NotFound(code, message));

    private static ServiceResult<ProductIssueReportDto> BadRequest(string code, string message) =>
        ServiceResult<ProductIssueReportDto>.Failure(Error.Validation(code, message));

    private static ServiceResult<ProductIssueReportDto> Forbidden() =>
        ServiceResult<ProductIssueReportDto>.Forbidden(ForbiddenMessage);

    private static ServiceResult<ProductIssueReportListResponseDto> BadRequestList(string code, string message) =>
        ServiceResult<ProductIssueReportListResponseDto>.Failure(Error.Validation(code, message));
}

internal sealed class ProductIssueEvidenceUploadPayload : IFileUploadPayload
{
    public ProductIssueEvidenceUploadPayload(ProductIssueEvidenceUploadDto source)
    {
        Content = source.Content;
        OriginalFileName = source.OriginalFileName;
        ContentType = source.ContentType ?? string.Empty;
        FileSizeBytes = source.FileSizeBytes;
    }

    public Stream Content { get; }
    public string OriginalFileName { get; }
    public string ContentType { get; }
    public long FileSizeBytes { get; }
}
