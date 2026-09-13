using FurniSpace.Application.Common;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.ProductIssues;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Storage;
using static FurniSpace.Application.Constants.ProductIssues.ProductIssueServiceConstants;

namespace FurniSpace.Application.Services.ProductIssues;

public sealed partial class DeliveryProductIssueReportService
{
    public async Task<ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>> PrepareEvidenceUploadAsync(
        Guid orderId,
        Guid currentUserId,
        PrepareProductIssueEvidenceUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var orderAccessError = await ValidateCustomerOrderEvidenceAccessAsync(orderId, currentUserId, cancellationToken);
        if (orderAccessError is not null)
        {
            return orderAccessError;
        }

        var fileValidation = _fileUploadValidator.ValidateMetadata(
            request.OriginalFileName,
            request.ContentType,
            request.FileSizeBytes);
        if (!fileValidation.IsValid)
        {
            return MapEvidencePrepareValidationResult(fileValidation);
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.NotFound(OrderNotFoundMessage);
        }

        var now = DateTime.UtcNow;
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var originalFileName = Path.GetFileName(request.OriginalFileName.Trim());
        var generatedFileName = ProjectFileUploadSupport.BuildGeneratedFileName(fileId, originalFileName);
        var objectName = ProjectFileUploadSupport.BuildProjectObjectName(
            _firebaseSettings,
            order.ProjectId,
            generatedFileName);
        var contentType = ProjectFileUploadSupport.NormalizeContentType(request.ContentType);

        var storedFile = _directUploadCoordinator.CreatePendingStoredFile(
            new DirectUploadPendingFileRequest(
                fileId,
                currentUserId,
                originalFileName,
                generatedFileName,
                objectName,
                contentType,
                request.FileSizeBytes,
                now));

        var fileLink = new FileLink
        {
            FileLinkId = fileLinkId,
            FileId = fileId,
            ReferenceType = OrderReferenceType,
            ReferenceId = orderId,
            FileType = FileType.PRODUCT_ISSUE_EVIDENCE,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            CreatedBy = currentUserId,
            CreatedAt = now
        };

        await ExecuteEvidenceUploadTransactionAsync(
            async ct =>
            {
                await _files.AddAsync(storedFile, ct);
                await _files.AddFileLinkAsync(fileLink, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        try
        {
            var signedUpload = await _directUploadCoordinator.CreateSignedUploadUrlAsync(
                objectName,
                contentType,
                cancellationToken);

            return ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.Created(
                new PrepareProductIssueEvidenceUploadResponseDto
                {
                    FileId = fileId,
                    OrderId = orderId,
                    UploadUrl = signedUpload.UploadUrl,
                    ContentType = signedUpload.ContentType,
                    ExpiresAt = signedUpload.ExpiresAt
                },
                "Product issue evidence upload URL created successfully.");
        }
        catch
        {
            await _directUploadCoordinator.DeleteObjectIfExistsAsync(objectName, cancellationToken);
            _files.Remove(storedFile);
            _files.RemoveFileLinks([fileLink]);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>> CompleteEvidenceUploadAsync(
        Guid orderId,
        Guid currentUserId,
        CompleteProductIssueEvidenceUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.BadRequest("Order id is required.");
        }

        if (request.FileId == Guid.Empty)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.BadRequest("File id is required.");
        }

        var orderAccessError = await ValidateCustomerOrderEvidenceAccessAsync(orderId, currentUserId, cancellationToken);
        if (orderAccessError is not null)
        {
            return MapEvidenceCompleteAccessFailure(orderAccessError);
        }

        var storedFile = await _files.GetByIdAsync(request.FileId, cancellationToken);
        if (storedFile is null)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Evidence upload not found."));
        }

        var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(request.FileId, cancellationToken);
        var orderLink = fileLinks.FirstOrDefault(link =>
            string.Equals(link.ReferenceType, OrderReferenceType, StringComparison.OrdinalIgnoreCase) &&
            link.ReferenceId == orderId &&
            link.FileType == FileType.PRODUCT_ISSUE_EVIDENCE);
        if (orderLink is null)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Failure(
                Error.NotFound(DirectFileUploadErrorCodes.UploadNotFound, "Evidence upload not found."));
        }

        if (storedFile.UploadedBy != currentUserId)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Failure(
                Error.Forbidden(
                    DirectFileUploadErrorCodes.UploadForbidden,
                    "You can only complete your own direct upload session."));
        }

        if (storedFile.Status == FileStatus.ACTIVE)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Success(
                BuildEvidenceUploadResponse(orderId, storedFile, orderLink),
                "Product issue evidence uploaded successfully.");
        }

        if (storedFile.Status != FileStatus.PENDING)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Failure(
                Error.Conflict(
                    DirectFileUploadErrorCodes.UploadNotPending,
                    "Evidence upload is not pending completion."));
        }

        var finalizeResult = await _directUploadCoordinator.TryFinalizeAsync(storedFile, cancellationToken);
        if (finalizeResult.Status is not (200 or 201) || finalizeResult.Data is null)
        {
            return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Failure(
                Error.Conflict(
                    finalizeResult.ErrorCode ?? DirectFileUploadErrorCodes.UploadObjectMissing,
                    finalizeResult.Message ?? "Direct upload finalization failed."));
        }

        _directUploadCoordinator.ActivateStoredFile(storedFile, finalizeResult.Data);

        await ExecuteEvidenceUploadTransactionAsync(
            async ct =>
            {
                _files.Update(storedFile);
                await _unitOfWork.SaveChangesAsync(ct);
            },
            cancellationToken);

        return ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>.Success(
            BuildEvidenceUploadResponse(orderId, storedFile, orderLink),
            "Product issue evidence uploaded successfully.");
    }

    private async Task<ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>?> ValidateCustomerOrderEvidenceAccessAsync(
        Guid orderId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.BadRequest("Order id is required.");
        }

        var roleName = await _projects.GetAccountRoleNameAsync(currentUserId, cancellationToken);
        if (!IsCustomer(roleName))
        {
            return ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.Forbidden(ForbiddenMessage);
        }

        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.NotFound(OrderNotFoundMessage);
        }

        var project = await _projects.GetByIdAsync(order.ProjectId, cancellationToken);
        if (project is null || project.CustomerId != currentUserId)
        {
            return ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.Forbidden(ForbiddenMessage);
        }

        return null;
    }

    private async Task<ServiceResult<ProductIssueReportDto>?> ValidateDraftEvidenceFilesAsync(
        Guid orderId,
        Guid currentUserId,
        IReadOnlyList<Guid> evidenceFileIds,
        CancellationToken cancellationToken)
    {
        if (evidenceFileIds.Count == 0)
        {
            return null;
        }

        foreach (var fileId in evidenceFileIds.Distinct())
        {
            if (fileId == Guid.Empty)
            {
                return BadRequest(
                    ProductIssueErrorCodes.EvidenceFileInvalid,
                    "Evidence file id is invalid.");
            }

            var storedFile = await _files.GetByIdAsync(fileId, cancellationToken);
            if (storedFile is null || storedFile.Status != FileStatus.ACTIVE || storedFile.UploadedBy != currentUserId)
            {
                return BadRequest(
                    ProductIssueErrorCodes.EvidenceFileInvalid,
                    "One or more evidence files are invalid for this order.");
            }

            var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
            var orderLink = fileLinks.FirstOrDefault(link =>
                string.Equals(link.ReferenceType, OrderReferenceType, StringComparison.OrdinalIgnoreCase) &&
                link.ReferenceId == orderId &&
                link.FileType == FileType.PRODUCT_ISSUE_EVIDENCE);
            if (orderLink is null)
            {
                return BadRequest(
                    ProductIssueErrorCodes.EvidenceFileInvalid,
                    "One or more evidence files are invalid for this order.");
            }
        }

        return null;
    }

    private async Task RelinkEvidenceFilesToIssueAsync(
        Guid issueId,
        IReadOnlyList<Guid> evidenceFileIds,
        CancellationToken cancellationToken)
    {
        foreach (var fileId in evidenceFileIds.Distinct())
        {
            var fileLinks = await _files.GetFileLinkEntitiesByFileIdAsync(fileId, cancellationToken);
            var orderLink = fileLinks.FirstOrDefault(link =>
                link.FileType == FileType.PRODUCT_ISSUE_EVIDENCE &&
                string.Equals(link.ReferenceType, OrderReferenceType, StringComparison.OrdinalIgnoreCase));
            if (orderLink is null)
            {
                continue;
            }

            orderLink.ReferenceType = IssueReportReferenceType;
            orderLink.ReferenceId = issueId;
        }
    }

    private static CompleteProductIssueEvidenceUploadResponseDto BuildEvidenceUploadResponse(
        Guid orderId,
        StoredFile storedFile,
        FileLink fileLink)
    {
        return new CompleteProductIssueEvidenceUploadResponseDto
        {
            FileId = storedFile.FileId,
            FileLinkId = fileLink.FileLinkId,
            OrderId = orderId,
            OriginalFileName = storedFile.OriginalFileName,
            FileUrl = storedFile.FileUrl,
            MimeType = storedFile.MimeType,
            FileSizeBytes = storedFile.FileSizeBytes
        };
    }

    private static ServiceResult<CompleteProductIssueEvidenceUploadResponseDto> MapEvidenceCompleteAccessFailure(
        ServiceResult<PrepareProductIssueEvidenceUploadResponseDto> source)
    {
        return new ServiceResult<CompleteProductIssueEvidenceUploadResponseDto>(source.Status, source.Message ?? "Request failed")
        {
            ErrorCode = source.ErrorCode,
            Errors = source.Errors
        };
    }

    private static ServiceResult<PrepareProductIssueEvidenceUploadResponseDto> MapEvidencePrepareValidationResult(
        FileUploadValidationResult validation)
    {
        return validation.FailureKind switch
        {
            FileUploadValidationFailureKind.FileTooLarge =>
                ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.PayloadTooLarge(validation.Message),
            FileUploadValidationFailureKind.InvalidExtension or FileUploadValidationFailureKind.InvalidMimeType =>
                ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.UnsupportedMediaType(validation.Message),
            _ => ServiceResult<PrepareProductIssueEvidenceUploadResponseDto>.BadRequest(validation.Message)
        };
    }

    private async Task ExecuteEvidenceUploadTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
