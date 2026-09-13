#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Shared;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class FilesControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(FilesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
    }

    [Fact]
    public async Task GetFileDetail_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        var controller = CreateController(new FakeProjectFileService(), userId: null);

        var actionResult = await controller.GetFileDetail(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task GetFileDetail_ReturnsServiceResultThroughBaseController()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var response = new FileDetailResponseDto
        {
            FileId = fileId,
            OriginalFileName = "shop-reference.jpg",
            FileName = "generated-storage-name.jpg",
            FileType = FileType.REFERENCE_IMAGE,
            MimeType = "image/jpeg",
            FileSize = 204800,
            StoragePath = "projects/project-id/generated-storage-name.jpg",
            PublicUrl = "https://storage.example.com/file.jpg",
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            Status = FileStatus.ACTIVE
        };
        var service = new FakeProjectFileService(
            fileDetailResult: ServiceResult<FileDetailResponseDto>.Success(response, "File detail retrieved successfully."));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetFileDetail(fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<FileDetailResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(fileId, service.FileId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public void GetFilesByReference_AllowsAnonymousAccess()
    {
        var method = typeof(FilesController)
            .GetMethod(nameof(FilesController.GetFilesByReference));

        Assert.NotNull(method);
        var allowAnonymous = method!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
            .Cast<AllowAnonymousAttribute>()
            .SingleOrDefault();

        Assert.NotNull(allowAnonymous);
    }

    [Fact]
    public async Task GetFilesByReference_WithoutUserId_PassesEmptyUserIdToService()
    {
        var referenceId = Guid.NewGuid();
        var response = new FilesByReferenceResponseDto
        {
            ReferenceType = "PRODUCT",
            ReferenceId = referenceId
        };
        var service = new FakeProjectFileService(
            byReferenceResult: ServiceResult<FilesByReferenceResponseDto>.Success(response, "Files retrieved successfully."));
        var controller = CreateController(service, userId: null);

        var actionResult = await controller.GetFilesByReference("PRODUCT", referenceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(Guid.Empty, service.CurrentUserId);
    }

    [Fact]
    public async Task GetFilesByReference_PassesQueryToService()
    {
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var response = new FilesByReferenceResponseDto
        {
            ReferenceType = "PROJECT",
            ReferenceId = referenceId,
            Page = 2,
            Limit = 10,
            Total = 1
        };
        var service = new FakeProjectFileService(
            byReferenceResult: ServiceResult<FilesByReferenceResponseDto>.Success(response, "Files retrieved successfully."));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetFilesByReference(
            "PROJECT",
            referenceId,
            FileType.MEASUREMENT_REPORT,
            FileVisibility.STAFF_ONLY,
            page: 2,
            limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<FilesByReferenceResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.NotNull(service.FilesByReferenceQuery);
        Assert.Equal("PROJECT", service.FilesByReferenceQuery.ReferenceType);
        Assert.Equal(referenceId, service.FilesByReferenceQuery.ReferenceId);
        Assert.Equal(FileType.MEASUREMENT_REPORT, service.FilesByReferenceQuery.FileType);
        Assert.Equal(FileVisibility.STAFF_ONLY, service.FilesByReferenceQuery.Visibility);
        Assert.Equal(2, service.FilesByReferenceQuery.Page);
        Assert.Equal(10, service.FilesByReferenceQuery.Limit);
    }

    [Fact]
    public async Task ArchiveFile_PassesRequestToService()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var request = new ArchiveFileRequestDto { Reason = "Outdated design reference." };
        var response = new ArchiveFileResponseDto
        {
            FileId = fileId,
            Status = FileStatus.ARCHIVED,
            ArchivedAt = DateTime.UtcNow
        };
        var service = new FakeProjectFileService(
            archiveResult: ServiceResult<ArchiveFileResponseDto>.Success(response, "File archived successfully."));
        var controller = CreateController(service, userId);

        var actionResult = await controller.ArchiveFile(fileId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ArchiveFileResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(fileId, service.FileId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Same(request, service.ArchiveRequest);
    }

    [Fact]
    public async Task DeleteFile_ReturnsServiceResultThroughBaseController()
    {
        var userId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var response = new DeleteFileResponseDto
        {
            FileId = fileId,
            DeletedAt = DateTime.UtcNow
        };
        var service = new FakeProjectFileService(
            deleteResult: ServiceResult<DeleteFileResponseDto>.Success(response, "File deleted successfully."));
        var controller = CreateController(service, userId);

        var actionResult = await controller.DeleteFile(fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<DeleteFileResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(fileId, service.FileId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    private static FilesController CreateController(FakeProjectFileService service, Guid? userId)
    {
        var controller = new FilesController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildUser(userId)
            }
        };

        return controller;
    }

    private static ClaimsPrincipal BuildUser(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
        ], "Test"));
    }

    private sealed class FakeProjectFileService : IProjectFileService
    {
        private readonly ServiceResult<ProjectFileUploadResponseDto> _uploadResult;
        private readonly ServiceResult<FileDetailResponseDto> _fileDetailResult;
        private readonly ServiceResult<ProjectFilesResponseDto> _projectFilesResult;
        private readonly ServiceResult<FilesByReferenceResponseDto> _byReferenceResult;
        private readonly ServiceResult<DeleteFileResponseDto> _deleteResult;
        private readonly ServiceResult<ArchiveFileResponseDto> _archiveResult;

        public FakeProjectFileService(
            ServiceResult<ProjectFileUploadResponseDto>? uploadResult = null,
            ServiceResult<FileDetailResponseDto>? fileDetailResult = null,
            ServiceResult<ProjectFilesResponseDto>? projectFilesResult = null,
            ServiceResult<FilesByReferenceResponseDto>? byReferenceResult = null,
            ServiceResult<DeleteFileResponseDto>? deleteResult = null,
            ServiceResult<ArchiveFileResponseDto>? archiveResult = null)
        {
            _uploadResult = uploadResult ?? ServiceResult<ProjectFileUploadResponseDto>.Created(new ProjectFileUploadResponseDto());
            _fileDetailResult = fileDetailResult ?? ServiceResult<FileDetailResponseDto>.Success(new FileDetailResponseDto());
            _projectFilesResult = projectFilesResult ?? ServiceResult<ProjectFilesResponseDto>.Success(new ProjectFilesResponseDto());
            _byReferenceResult = byReferenceResult ?? ServiceResult<FilesByReferenceResponseDto>.Success(new FilesByReferenceResponseDto());
            _deleteResult = deleteResult ?? ServiceResult<DeleteFileResponseDto>.Success(new DeleteFileResponseDto());
            _archiveResult = archiveResult ?? ServiceResult<ArchiveFileResponseDto>.Success(new ArchiveFileResponseDto());
        }

        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid FileId { get; private set; }
        public UploadProjectFileRequestDto? UploadRequest { get; private set; }
        public ProjectFilesQueryDto? ProjectFilesQuery { get; private set; }
        public FilesByReferenceQueryDto? FilesByReferenceQuery { get; private set; }
        public ArchiveFileRequestDto? ArchiveRequest { get; private set; }

        public Task<ServiceResult<ProjectFileUploadResponseDto>> UploadProjectFileAsync(
            Guid projectId,
            Guid currentUserId,
            UploadProjectFileRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            UploadRequest = request;
            return Task.FromResult(_uploadResult);
        }

        public Task<ServiceResult<FileDetailResponseDto>> GetFileDetailAsync(
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            FileId = fileId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_fileDetailResult);
        }

        public Task<ServiceResult<ProjectFilesResponseDto>> GetProjectFilesAsync(
            Guid projectId,
            Guid currentUserId,
            ProjectFilesQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            ProjectFilesQuery = query;
            return Task.FromResult(_projectFilesResult);
        }

        public Task<ServiceResult<ProjectFileSearchResponseDto>> SearchProjectFilesAsync(
            Guid projectId,
            Guid currentUserId,
            string query,
            int page,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            _ = query;
            _ = page;
            _ = limit;
            return Task.FromResult(ServiceResult<ProjectFileSearchResponseDto>.Success(
                new ProjectFileSearchResponseDto(),
                string.Empty));
        }

        public Task<ServiceResult<FilesByReferenceResponseDto>> GetFilesByReferenceAsync(
            Guid currentUserId,
            FilesByReferenceQueryDto query,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            FilesByReferenceQuery = query;
            return Task.FromResult(_byReferenceResult);
        }

        public Task<ServiceResult<DeleteFileResponseDto>> DeleteFileAsync(
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            FileId = fileId;
            CurrentUserId = currentUserId;
            return Task.FromResult(_deleteResult);
        }

        public Task<ServiceResult<ArchiveFileResponseDto>> ArchiveFileAsync(
            Guid fileId,
            Guid currentUserId,
            ArchiveFileRequestDto request,
            CancellationToken cancellationToken = default)
        {
            FileId = fileId;
            CurrentUserId = currentUserId;
            ArchiveRequest = request;
            return Task.FromResult(_archiveResult);
        }
    }
}
