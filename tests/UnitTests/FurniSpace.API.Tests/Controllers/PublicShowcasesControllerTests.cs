#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Public;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectShowcases;
using FurniSpace.Application.Interfaces.ProjectShowcases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class PublicShowcasesControllerTests
{
    [Fact]
    public void Controller_AllowsAnonymousAccess()
    {
        var authorize = typeof(PublicShowcasesController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
            .Cast<AllowAnonymousAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
    }

    [Fact]
    public async Task GetList_ReturnsServiceResult()
    {
        var response = new PublicShowcaseListResponseDto();
        var service = new FakeProjectShowcaseService(
            publicListResult: ServiceResult<PublicShowcaseListResponseDto>.Success(response));
        var controller = new PublicShowcasesController(service);

        var actionResult = await controller.GetList(new PublicShowcaseQueryDto { Page = 1, PageSize = 12 });

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Same(response, Assert.IsType<ServiceResult<PublicShowcaseListResponseDto>>(objectResult.Value).Data);
    }

    [Fact]
    public async Task GetBySlug_PassesSlugToService()
    {
        var response = new PublicShowcaseDetailDto { Slug = "cafe-makeover" };
        var service = new FakeProjectShowcaseService(
            publicDetailResult: ServiceResult<PublicShowcaseDetailDto>.Success(response));
        var controller = new PublicShowcasesController(service);

        var actionResult = await controller.GetBySlug("cafe-makeover");

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal("cafe-makeover", service.PublicSlug);
    }

    private sealed class FakeProjectShowcaseService : IProjectShowcaseService
    {
        private readonly ServiceResult<PublicShowcaseListResponseDto>? _publicListResult;
        private readonly ServiceResult<PublicShowcaseDetailDto>? _publicDetailResult;

        public FakeProjectShowcaseService(
            ServiceResult<PublicShowcaseListResponseDto>? publicListResult = null,
            ServiceResult<PublicShowcaseDetailDto>? publicDetailResult = null)
        {
            _publicListResult = publicListResult;
            _publicDetailResult = publicDetailResult;
        }

        public string? PublicSlug { get; private set; }

        public Task<ServiceResult<PublicShowcaseListResponseDto>> GetPublicListAsync(
            PublicShowcaseQueryDto query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_publicListResult ?? ServiceResult<PublicShowcaseListResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<PublicShowcaseDetailDto>> GetPublicBySlugAsync(
            string slug,
            CancellationToken cancellationToken = default)
        {
            PublicSlug = slug;
            return Task.FromResult(_publicDetailResult ?? ServiceResult<PublicShowcaseDetailDto>.Unauthorized());
        }

        public Task<ServiceResult<ProjectShowcaseDto>> CreateAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectShowcaseRequestDto? request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> GetByProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseMediaDto>> SetCoverAsync(
            Guid showcaseId,
            Guid mediaId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseMediaDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> RemoveMediaAsync(
            Guid showcaseId,
            Guid mediaId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> UpdateAsync(
            Guid showcaseId,
            Guid currentUserId,
            UpdateProjectShowcaseRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> SubmitAsync(
            Guid showcaseId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> PublishAsync(
            Guid showcaseId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> ArchiveAsync(
            Guid showcaseId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseMediaDto>> AddMediaAsync(
            Guid showcaseId,
            Guid currentUserId,
            AddProjectShowcaseMediaRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseMediaDto>.Unauthorized());

        public Task<ServiceResult<ProjectShowcaseDto>> ReorderMediaAsync(
            Guid showcaseId,
            Guid currentUserId,
            ReorderProjectShowcaseMediaRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<ProjectShowcaseDto>.Unauthorized());

    }
}
