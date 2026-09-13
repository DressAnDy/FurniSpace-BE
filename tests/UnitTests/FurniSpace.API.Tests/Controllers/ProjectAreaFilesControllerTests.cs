#nullable enable

using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Application.Interfaces.ProjectFiles;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectAreaFilesControllerTests
{
    [Fact]
    public async Task PrepareProjectAreaFileUpload_PassesRequestToService()
    {
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var response = new ProjectFileUploadResponseDto { ReferenceType = "PROJECT_AREA", ReferenceId = areaId };
        var service = new FakeProjectFileService
        {
            PrepareAreaResult = ServiceResult<PrepareProjectAreaFileUploadResponseDto>.Created(
                new PrepareProjectAreaFileUploadResponseDto
                {
                    FileId = Guid.NewGuid(),
                    ProjectAreaId = areaId,
                    ProjectId = Guid.NewGuid(),
                    UploadUrl = "https://storage.example.com/upload",
                    ContentType = "application/pdf",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15)
                },
                "Project area file upload URL created successfully.")
        };
        var controller = CreateController(service, userId);
        var request = new PrepareProjectFileUploadRequestDto
        {
            OriginalFileName = "area.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 12,
            FileType = FileType.PDF_DRAWING,
            Visibility = FileVisibility.STAFF_ONLY,
            IsPrimary = true,
            DisplayOrder = 3
        };

        var actionResult = await controller.PrepareProjectAreaFileUpload(areaId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(areaId, service.ProjectAreaId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.True(service.PrepareAreaRequest!.IsPrimary);
        Assert.Equal(3, service.PrepareAreaRequest.DisplayOrder);
    }

    [Fact]
    public async Task GetProjectAreaFiles_PassesQueryToService()
    {
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var service = new FakeProjectFileService
        {
            AreaFilesResult = ServiceResult<ProjectFilesResponseDto>.Success(new ProjectFilesResponseDto())
        };
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetProjectAreaFiles(
            areaId,
            new ProjectFilesQueryDto
            {
                FileType = FileType.FLOOR_PLAN,
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                Page = 2,
                Limit = 10
            });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(areaId, service.ProjectAreaId);
        Assert.Equal(FileType.FLOOR_PLAN, service.AreaFilesQuery!.FileType);
        Assert.Equal(FileVisibility.CUSTOMER_VISIBLE, service.AreaFilesQuery.Visibility);
        Assert.Equal(2, service.AreaFilesQuery.Page);
        Assert.Equal(10, service.AreaFilesQuery.Limit);
    }

    [Fact]
    public async Task SetPrimary_PassesFileToService()
    {
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeProjectFileService
        {
            PrimaryResult = ServiceResult<ProjectAreaFilePrimaryResponseDto>.Success(
                new ProjectAreaFilePrimaryResponseDto { ProjectAreaId = areaId, FileId = fileId })
        };
        var controller = CreateController(service, userId);

        var actionResult = await controller.SetPrimaryFile(areaId, fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(areaId, service.ProjectAreaId);
        Assert.Equal(fileId, service.FileId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task CompleteProjectAreaFileUpload_PassesRequestToService()
    {
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeProjectFileService
        {
            CompleteAreaResult = ServiceResult<ProjectFileUploadResponseDto>.Created(
                new ProjectFileUploadResponseDto
                {
                    FileId = fileId,
                    ReferenceType = "PROJECT_AREA",
                    ReferenceId = areaId
                },
                "Project area file uploaded successfully.")
        };
        var controller = CreateController(service, userId);

        var actionResult = await controller.CompleteProjectAreaFileUpload(
            areaId,
            new CompleteProjectFileUploadRequestDto { FileId = fileId });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(areaId, service.ProjectAreaId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.Equal(fileId, service.CompleteAreaRequest!.FileId);
    }

    [Fact]
    public async Task PrepareProjectAreaFileUpload_ReturnsUnauthorized_WhenClaimMissing()
    {
        var controller = CreateController(new FakeProjectFileService(), userId: null);

        var actionResult = await controller.PrepareProjectAreaFileUpload(Guid.NewGuid(), new PrepareProjectFileUploadRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static ProjectAreaFilesController CreateController(FakeProjectFileService service, Guid? userId)
    {
        return new ProjectAreaFilesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = BuildUser(userId) }
            }
        };
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

    private static FormFile CreateFormFile(string fileName)
    {
        var stream = new MemoryStream("file"u8.ToArray());
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private sealed class FakeProjectFileService : IProjectFileService
    {
        public Guid ProjectAreaId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public Guid FileId { get; private set; }
        public PrepareProjectFileUploadRequestDto? PrepareAreaRequest { get; private set; }
        public CompleteProjectFileUploadRequestDto? CompleteAreaRequest { get; private set; }
        public ProjectFilesQueryDto? AreaFilesQuery { get; private set; }
        public ServiceResult<PrepareProjectAreaFileUploadResponseDto> PrepareAreaResult { get; init; } =
            ServiceResult<PrepareProjectAreaFileUploadResponseDto>.Created(new PrepareProjectAreaFileUploadResponseDto());
        public ServiceResult<ProjectFileUploadResponseDto> CompleteAreaResult { get; init; } =
            ServiceResult<ProjectFileUploadResponseDto>.Created(new ProjectFileUploadResponseDto());
        public ServiceResult<ProjectFilesResponseDto> AreaFilesResult { get; init; } =
            ServiceResult<ProjectFilesResponseDto>.Success(new ProjectFilesResponseDto());
        public ServiceResult<ProjectAreaFilePrimaryResponseDto> PrimaryResult { get; init; } =
            ServiceResult<ProjectAreaFilePrimaryResponseDto>.Success(new ProjectAreaFilePrimaryResponseDto());

        public Task<ServiceResult<PrepareProjectAreaFileUploadResponseDto>> PrepareProjectAreaFileUploadAsync(
            Guid projectAreaId,
            Guid currentUserId,
            PrepareProjectFileUploadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectAreaId = projectAreaId;
            CurrentUserId = currentUserId;
            PrepareAreaRequest = request;
            return Task.FromResult(PrepareAreaResult);
        }

        public Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectAreaFileUploadAsync(
            Guid projectAreaId,
            Guid currentUserId,
            CompleteProjectFileUploadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectAreaId = projectAreaId;
            CurrentUserId = currentUserId;
            CompleteAreaRequest = request;
            return Task.FromResult(CompleteAreaResult);
        }

        public Task<ServiceResult<ProjectFilesResponseDto>> GetProjectAreaFilesAsync(
            Guid projectAreaId,
            Guid currentUserId,
            ProjectFilesQueryDto query,
            CancellationToken cancellationToken = default)
        {
            ProjectAreaId = projectAreaId;
            CurrentUserId = currentUserId;
            AreaFilesQuery = query;
            return Task.FromResult(AreaFilesResult);
        }

        public Task<ServiceResult<ProjectAreaFilePrimaryResponseDto>> SetProjectAreaPrimaryFileAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            ProjectAreaId = projectAreaId;
            FileId = fileId;
            CurrentUserId = currentUserId;
            return Task.FromResult(PrimaryResult);
        }

        public Task<ServiceResult<PrepareProjectFileUploadResponseDto>> PrepareProjectFileUploadAsync(Guid projectId, Guid currentUserId, PrepareProjectFileUploadRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProjectFileUploadResponseDto>> CompleteProjectFileUploadAsync(Guid projectId, Guid currentUserId, CompleteProjectFileUploadRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FileDetailResponseDto>> GetFileDetailAsync(Guid fileId, Guid currentUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProjectFilesResponseDto>> GetProjectFilesAsync(Guid projectId, Guid currentUserId, ProjectFilesQueryDto query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProjectFileSearchResponseDto>> SearchProjectFilesAsync(Guid projectId, Guid currentUserId, string query, int page, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FilesByReferenceResponseDto>> GetFilesByReferenceAsync(Guid currentUserId, FilesByReferenceQueryDto query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<DeleteFileResponseDto>> DeleteFileAsync(Guid fileId, Guid currentUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ArchiveFileResponseDto>> ArchiveFileAsync(Guid fileId, Guid currentUserId, ArchiveFileRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
