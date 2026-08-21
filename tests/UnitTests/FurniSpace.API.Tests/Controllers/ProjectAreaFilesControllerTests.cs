#nullable enable

using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.API.DTOs.ProjectFiles;
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
    public async Task UploadProjectAreaFile_PassesMultipartRequestToService()
    {
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var response = new ProjectFileUploadResponseDto { ReferenceType = "PROJECT_AREA", ReferenceId = areaId };
        var service = new FakeProjectFileService
        {
            UploadAreaResult = ServiceResult<ProjectFileUploadResponseDto>.Created(
                response,
                "Project area file uploaded successfully.")
        };
        var controller = CreateController(service, userId);
        var request = new UploadProjectFileFormRequest
        {
            File = CreateFormFile("area.pdf"),
            FileType = FileType.PDF_DRAWING,
            Visibility = FileVisibility.STAFF_ONLY,
            IsPrimary = true,
            DisplayOrder = 3
        };

        var actionResult = await controller.UploadProjectAreaFile(areaId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(areaId, service.ProjectAreaId);
        Assert.Equal(userId, service.CurrentUserId);
        Assert.True(service.UploadRequest!.IsPrimary);
        Assert.Equal(3, service.UploadRequest.DisplayOrder);
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
            FileType.FLOOR_PLAN,
            FileVisibility.CUSTOMER_VISIBLE,
            page: 2,
            limit: 10);

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

        var actionResult = await controller.SetPrimary(areaId, fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(areaId, service.ProjectAreaId);
        Assert.Equal(fileId, service.FileId);
        Assert.Equal(userId, service.CurrentUserId);
    }

    [Fact]
    public async Task UploadProjectAreaFile_ReturnsUnauthorized_WhenClaimMissing()
    {
        var controller = CreateController(new FakeProjectFileService(), userId: null);

        var actionResult = await controller.UploadProjectAreaFile(Guid.NewGuid(), new UploadProjectFileFormRequest());

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
        public UploadProjectFileRequestDto? UploadRequest { get; private set; }
        public ProjectFilesQueryDto? AreaFilesQuery { get; private set; }
        public ServiceResult<ProjectFileUploadResponseDto> UploadAreaResult { get; init; } =
            ServiceResult<ProjectFileUploadResponseDto>.Created(new ProjectFileUploadResponseDto());
        public ServiceResult<ProjectFilesResponseDto> AreaFilesResult { get; init; } =
            ServiceResult<ProjectFilesResponseDto>.Success(new ProjectFilesResponseDto());
        public ServiceResult<ProjectAreaFilePrimaryResponseDto> PrimaryResult { get; init; } =
            ServiceResult<ProjectAreaFilePrimaryResponseDto>.Success(new ProjectAreaFilePrimaryResponseDto());

        public Task<ServiceResult<ProjectFileUploadResponseDto>> UploadProjectAreaFileAsync(
            Guid projectAreaId,
            Guid currentUserId,
            UploadProjectFileRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProjectAreaId = projectAreaId;
            CurrentUserId = currentUserId;
            UploadRequest = request;
            return Task.FromResult(UploadAreaResult);
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

        public Task<ServiceResult<ProjectFileUploadResponseDto>> UploadProjectFileAsync(Guid projectId, Guid currentUserId, UploadProjectFileRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FileDetailResponseDto>> GetFileDetailAsync(Guid fileId, Guid currentUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProjectFilesResponseDto>> GetProjectFilesAsync(Guid projectId, Guid currentUserId, ProjectFilesQueryDto query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ProjectFileSearchResponseDto>> SearchProjectFilesAsync(Guid projectId, Guid currentUserId, string query, int page, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<FilesByReferenceResponseDto>> GetFilesByReferenceAsync(Guid currentUserId, FilesByReferenceQueryDto query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<DeleteFileResponseDto>> DeleteFileAsync(Guid fileId, Guid currentUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ServiceResult<ArchiveFileResponseDto>> ArchiveFileAsync(Guid fileId, Guid currentUserId, ArchiveFileRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
