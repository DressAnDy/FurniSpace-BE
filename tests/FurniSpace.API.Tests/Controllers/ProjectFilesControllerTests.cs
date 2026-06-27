#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.API.DTOs.ProjectFiles;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectFilesControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(ProjectFilesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
    }

    [Fact]
    public async Task UploadProjectFile_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        var controller = CreateController(new FakeProjectFileService(), userId: null);

        var actionResult = await controller.UploadProjectFile(
            Guid.NewGuid(),
            new UploadProjectFileFormRequest());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task UploadProjectFile_PassesMultipartRequestToService()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var fileLinkId = Guid.NewGuid();
        var response = new ProjectFileUploadResponseDto
        {
            FileId = fileId,
            FileLinkId = fileLinkId,
            ProjectId = projectId,
            OriginalFileName = "shop-reference.jpg",
            FileName = "generated-storage-name.jpg",
            FileType = FileType.REFERENCE_IMAGE,
            MimeType = "image/jpeg",
            FileSize = 12,
            StoragePath = "projects/project-id/generated-storage-name.jpg",
            PublicUrl = "https://storage.example.com/file.jpg",
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow
        };
        var service = new FakeProjectFileService(
            uploadResult: ServiceResult<ProjectFileUploadResponseDto>.Created(response, "Project file uploaded successfully."));
        var controller = CreateController(service, userId);
        var request = new UploadProjectFileFormRequest
        {
            File = CreateFormFile("shop-reference.jpg", "image/jpeg", "file-content"),
            FileType = FileType.REFERENCE_IMAGE,
            Visibility = FileVisibility.CUSTOMER_VISIBLE,
            Note = "Reference image"
        };

        var actionResult = await controller.UploadProjectFile(projectId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectFileUploadResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.NotNull(service.UploadRequest);
        Assert.Equal("shop-reference.jpg", service.UploadRequest.OriginalFileName);
        Assert.Equal("image/jpeg", service.UploadRequest.ContentType);
        Assert.Equal(12, service.UploadRequest.FileSizeBytes);
        Assert.Equal(FileType.REFERENCE_IMAGE, service.UploadRequest.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, service.UploadRequest.Visibility);
        Assert.Equal("Reference image", service.UploadRequest.Note);
        Assert.True(service.UploadRequest.Content.CanRead);
    }

    [Fact]
    public async Task GetProjectFiles_PassesQueryToService()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var response = new ProjectFilesResponseDto
        {
            Page = 3,
            Limit = 5,
            Total = 1
        };
        var service = new FakeProjectFileService(
            projectFilesResult: ServiceResult<ProjectFilesResponseDto>.Success(response, "Project files retrieved successfully."));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetProjectFiles(
            projectId,
            FileType.ORDER_DOCUMENT,
            FileVisibility.STAFF_ONLY,
            page: 3,
            limit: 5);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectFilesResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.NotNull(service.ProjectFilesQuery);
        Assert.Equal(FileType.ORDER_DOCUMENT, service.ProjectFilesQuery.FileType);
        Assert.Equal(FileVisibility.STAFF_ONLY, service.ProjectFilesQuery.Visibility);
        Assert.Equal(3, service.ProjectFilesQuery.Page);
        Assert.Equal(5, service.ProjectFilesQuery.Limit);
    }

    private static ProjectFilesController CreateController(FakeProjectFileService service, Guid? userId)
    {
        var controller = new ProjectFilesController(service);
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

    private static FormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
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
