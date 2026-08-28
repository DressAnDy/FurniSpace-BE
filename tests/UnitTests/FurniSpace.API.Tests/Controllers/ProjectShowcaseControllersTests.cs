#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.API.DTOs.ProjectShowcases;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectShowcaseControllersTests
{
    [Fact]
    public void ProjectShowcases_Create_RequiresSalesAndAdmin()
    {
        var authorize = GetMethodAuthorize<ProjectShowcasesController>(nameof(ProjectShowcasesController.Create));

        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task ProjectShowcases_Create_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var service = new FakeProjectShowcaseService(
            createResult: ServiceResult<ProjectShowcaseDto>.Created(
                new ProjectShowcaseDto { ProjectId = projectId, Status = ProjectShowcaseStatus.DRAFT },
                "Created"));
        var controller = WithUser(new ProjectShowcasesController(service), "SALES");

        var actionResult = await controller.Create(projectId, new CreateProjectShowcaseRequestDto { Title = "Cafe" });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(projectId, service.LastProjectId);
    }

    [Fact]
    public async Task ProjectShowcases_Get_ReturnsServiceResult()
    {
        var projectId = Guid.NewGuid();
        var service = new FakeProjectShowcaseService(
            getByProjectResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto { ProjectId = projectId }));
        var controller = WithUser(new ProjectShowcasesController(service), "DESIGNER");

        var actionResult = await controller.Get(projectId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public void Workflow_Publish_RequiresAdmin()
    {
        var authorize = GetMethodAuthorize<ProjectShowcaseWorkflowController>(nameof(ProjectShowcaseWorkflowController.Publish));

        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Workflow_Update_Submit_Publish_Archive_ReturnServiceResults()
    {
        var showcaseId = Guid.NewGuid();
        var service = new FakeProjectShowcaseService(
            updateResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto { ProjectShowcaseId = showcaseId }),
            submitResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto { Status = ProjectShowcaseStatus.PENDING_REVIEW }),
            publishResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto { Status = ProjectShowcaseStatus.PUBLISHED }),
            archiveResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto { Status = ProjectShowcaseStatus.ARCHIVED }));
        var controller = WithUser(new ProjectShowcaseWorkflowController(service), "ADMIN");

        var updateResult = await controller.Update(showcaseId, new UpdateProjectShowcaseRequestDto { Title = "Updated" });
        var submitResult = await controller.Submit(showcaseId);
        var publishResult = await controller.Publish(showcaseId);
        var archiveResult = await controller.Archive(showcaseId);

        Assert.Equal(200, Assert.IsType<ObjectResult>(updateResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(submitResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(publishResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(archiveResult).StatusCode);
        Assert.Equal(showcaseId, service.LastShowcaseId);
    }

    [Fact]
    public void Media_Upload_RequiresSalesDesignerAndAdmin()
    {
        var authorize = GetMethodAuthorize<ProjectShowcaseMediaController>(nameof(ProjectShowcaseMediaController.Upload));

        Assert.Equal("SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task Media_Upload_PassesMultipartRequestToService()
    {
        var showcaseId = Guid.NewGuid();
        var service = new FakeProjectShowcaseService(
            uploadMediaResult: ServiceResult<ProjectShowcaseMediaDto>.Created(
                new ProjectShowcaseMediaDto { IsCover = true, MediaType = ProjectShowcaseMediaType.FINAL },
                "Uploaded"));
        var controller = WithUser(new ProjectShowcaseMediaController(service), "DESIGNER");
        var request = new UploadProjectShowcaseMediaFormRequest
        {
            File = CreateFormFile("showcase.jpg", "image/jpeg", "file-content"),
            MediaType = ProjectShowcaseMediaType.FINAL,
            Title = "Cover",
            SetAsCover = true
        };

        var actionResult = await controller.Upload(showcaseId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(showcaseId, service.LastShowcaseId);
        Assert.NotNull(service.LastUploadRequest);
        Assert.Equal("showcase.jpg", service.LastUploadRequest!.OriginalFileName);
    }

    [Fact]
    public async Task Media_Upload_WithoutUser_ReturnsUnauthorized()
    {
        var controller = new ProjectShowcaseMediaController(new FakeProjectShowcaseService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var actionResult = await controller.Upload(
            Guid.NewGuid(),
            new UploadProjectShowcaseMediaFormRequest());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task Media_Add_Reorder_SetCover_Remove_ReturnServiceResults()
    {
        var showcaseId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var service = new FakeProjectShowcaseService(
            addMediaResult: ServiceResult<ProjectShowcaseMediaDto>.Created(new ProjectShowcaseMediaDto(), "Added"),
            reorderMediaResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto()),
            setCoverResult: ServiceResult<ProjectShowcaseMediaDto>.Success(new ProjectShowcaseMediaDto { IsCover = true }),
            removeMediaResult: ServiceResult<ProjectShowcaseDto>.Success(new ProjectShowcaseDto()));
        var controller = WithUser(new ProjectShowcaseMediaController(service), "DESIGNER");

        var addResult = await controller.Add(showcaseId, new AddProjectShowcaseMediaRequestDto { FileId = Guid.NewGuid() });
        var reorderResult = await controller.Reorder(showcaseId, new ReorderProjectShowcaseMediaRequestDto { MediaIds = [mediaId] });
        var coverResult = await controller.SetCover(showcaseId, mediaId);
        var removeResult = await controller.Remove(showcaseId, mediaId);

        Assert.Equal(201, Assert.IsType<ObjectResult>(addResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(reorderResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(coverResult).StatusCode);
        Assert.Equal(200, Assert.IsType<ObjectResult>(removeResult).StatusCode);
    }

    [Fact]
    public async Task Workflow_Update_WithoutUser_ReturnsUnauthorized()
    {
        var controller = new ProjectShowcaseWorkflowController(new FakeProjectShowcaseService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var actionResult = await controller.Update(Guid.NewGuid(), new UpdateProjectShowcaseRequestDto());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    private static AuthorizeAttribute GetMethodAuthorize<TController>(string methodName)
    {
        return typeof(TController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
    }

    private static TController WithUser<TController>(TController controller, string role)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, role)
                ],
                authenticationType: "Test"))
            }
        };
        return controller;
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

    private sealed class FakeProjectShowcaseService : IProjectShowcaseService
    {
        private readonly ServiceResult<ProjectShowcaseDto>? _createResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _getByProjectResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _updateResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _submitResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _publishResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _archiveResult;
        private readonly ServiceResult<ProjectShowcaseMediaDto>? _addMediaResult;
        private readonly ServiceResult<ProjectShowcaseMediaDto>? _uploadMediaResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _reorderMediaResult;
        private readonly ServiceResult<ProjectShowcaseMediaDto>? _setCoverResult;
        private readonly ServiceResult<ProjectShowcaseDto>? _removeMediaResult;

        public FakeProjectShowcaseService(
            ServiceResult<ProjectShowcaseDto>? createResult = null,
            ServiceResult<ProjectShowcaseDto>? getByProjectResult = null,
            ServiceResult<ProjectShowcaseDto>? updateResult = null,
            ServiceResult<ProjectShowcaseDto>? submitResult = null,
            ServiceResult<ProjectShowcaseDto>? publishResult = null,
            ServiceResult<ProjectShowcaseDto>? archiveResult = null,
            ServiceResult<ProjectShowcaseMediaDto>? addMediaResult = null,
            ServiceResult<ProjectShowcaseMediaDto>? uploadMediaResult = null,
            ServiceResult<ProjectShowcaseDto>? reorderMediaResult = null,
            ServiceResult<ProjectShowcaseMediaDto>? setCoverResult = null,
            ServiceResult<ProjectShowcaseDto>? removeMediaResult = null)
        {
            _createResult = createResult;
            _getByProjectResult = getByProjectResult;
            _updateResult = updateResult;
            _submitResult = submitResult;
            _publishResult = publishResult;
            _archiveResult = archiveResult;
            _addMediaResult = addMediaResult;
            _uploadMediaResult = uploadMediaResult;
            _reorderMediaResult = reorderMediaResult;
            _setCoverResult = setCoverResult;
            _removeMediaResult = removeMediaResult;
        }

        public Guid LastProjectId { get; private set; }

        public Guid LastShowcaseId { get; private set; }

        public UploadProjectShowcaseMediaRequestDto? LastUploadRequest { get; private set; }

        public Task<ServiceResult<ProjectShowcaseDto>> CreateAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectShowcaseRequestDto? request,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            return Task.FromResult(_createResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastProjectId = projectId;
            return Task.FromResult(_getByProjectResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> UpdateAsync(
            Guid showcaseId,
            Guid currentUserId,
            UpdateProjectShowcaseRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_updateResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> SubmitAsync(
            Guid showcaseId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_submitResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> PublishAsync(
            Guid showcaseId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_publishResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> ArchiveAsync(
            Guid showcaseId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_archiveResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseMediaDto>> AddMediaAsync(
            Guid showcaseId,
            Guid currentUserId,
            AddProjectShowcaseMediaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_addMediaResult ?? ServiceResult<ProjectShowcaseMediaDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseMediaDto>> UploadMediaAsync(
            Guid showcaseId,
            Guid currentUserId,
            UploadProjectShowcaseMediaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            LastUploadRequest = request;
            return Task.FromResult(_uploadMediaResult ?? _addMediaResult ?? ServiceResult<ProjectShowcaseMediaDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> ReorderMediaAsync(
            Guid showcaseId,
            Guid currentUserId,
            ReorderProjectShowcaseMediaRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_reorderMediaResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseMediaDto>> SetCoverAsync(
            Guid showcaseId,
            Guid mediaId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_setCoverResult ?? ServiceResult<ProjectShowcaseMediaDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> RemoveMediaAsync(
            Guid showcaseId,
            Guid mediaId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastShowcaseId = showcaseId;
            return Task.FromResult(_removeMediaResult ?? ServiceResult<ProjectShowcaseDto>.Unauthorized());
        }

        public Task<ServiceResult<PublicShowcaseListResponseDto>> GetPublicListAsync(
            PublicShowcaseQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PublicShowcaseListResponseDto>.Unauthorized());

        public Task<ServiceResult<PublicShowcaseDetailDto>> GetPublicBySlugAsync(
            string slug,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<PublicShowcaseDetailDto>.Unauthorized());
    }
}
