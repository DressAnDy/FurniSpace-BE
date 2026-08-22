#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Projects;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Application.Interfaces.MeasurementImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectAreaMeasurementImagesControllerTests
{
    [Fact]
    public void GetMeasurementImages_AllowsProjectParticipantRoles()
    {
        var authorize = GetMethodAuthorize(nameof(ProjectAreaMeasurementImagesController.GetMeasurementImages));

        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public void LinkMeasurementImage_AllowsSalesDesignerAdmin()
    {
        var authorize = GetMethodAuthorize(nameof(ProjectAreaMeasurementImagesController.LinkMeasurementImage));

        Assert.Equal("SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetMeasurementImages_ReturnsServiceResult()
    {
        var projectAreaId = Guid.NewGuid();
        var response = new MeasurementImageGalleryResponseDto { Total = 1 };
        var service = new FakeMeasurementImageService(
            areaGalleryResult: ServiceResult<MeasurementImageGalleryResponseDto>.Success(response));
        var controller = WithUser(new ProjectAreaMeasurementImagesController(service));

        var actionResult = await controller.GetMeasurementImages(projectAreaId, page: 2, limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(projectAreaId, service.LastProjectAreaId);
        Assert.Equal(2, service.LastQuery?.Page);
        Assert.Equal(10, service.LastQuery?.Limit);
    }

    [Fact]
    public async Task LinkMeasurementImage_ReturnsServiceResult()
    {
        var projectAreaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeMeasurementImageService(
            linkResult: ServiceResult<MeasurementImageAreaLinkResponseDto>.Created(
                new MeasurementImageAreaLinkResponseDto { ProjectAreaId = projectAreaId, FileId = fileId },
                "Linked"));
        var controller = WithUser(new ProjectAreaMeasurementImagesController(service));

        var actionResult = await controller.LinkMeasurementImage(projectAreaId, fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(fileId, service.LastFileId);
    }

    [Fact]
    public async Task UnlinkMeasurementImage_WithoutUser_ReturnsUnauthorized()
    {
        var controller = new ProjectAreaMeasurementImagesController(new FakeMeasurementImageService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var actionResult = await controller.UnlinkMeasurementImage(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
    }

    [Fact]
    public async Task UnlinkMeasurementImage_ReturnsServiceResult()
    {
        var projectAreaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var service = new FakeMeasurementImageService(
            unlinkResult: ServiceResult<MeasurementImageAreaLinkResponseDto>.Success(
                new MeasurementImageAreaLinkResponseDto { ProjectAreaId = projectAreaId, FileId = fileId },
                "Unlinked"));
        var controller = WithUser(new ProjectAreaMeasurementImagesController(service));

        var actionResult = await controller.UnlinkMeasurementImage(projectAreaId, fileId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
    }

    private static AuthorizeAttribute GetMethodAuthorize(string methodName)
    {
        return typeof(ProjectAreaMeasurementImagesController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
    }

    private static ProjectAreaMeasurementImagesController WithUser(
        ProjectAreaMeasurementImagesController controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "DESIGNER")
                ],
                authenticationType: "Test"))
            }
        };
        return controller;
    }

    private sealed class FakeMeasurementImageService : IMeasurementImageService
    {
        private readonly ServiceResult<MeasurementImageGalleryResponseDto>? _areaGalleryResult;
        private readonly ServiceResult<MeasurementImageAreaLinkResponseDto>? _linkResult;
        private readonly ServiceResult<MeasurementImageAreaLinkResponseDto>? _unlinkResult;

        public FakeMeasurementImageService(
            ServiceResult<MeasurementImageGalleryResponseDto>? areaGalleryResult = null,
            ServiceResult<MeasurementImageAreaLinkResponseDto>? linkResult = null,
            ServiceResult<MeasurementImageAreaLinkResponseDto>? unlinkResult = null)
        {
            _areaGalleryResult = areaGalleryResult;
            _linkResult = linkResult;
            _unlinkResult = unlinkResult;
        }

        public Guid LastProjectAreaId { get; private set; }

        public Guid LastFileId { get; private set; }

        public MeasurementImageGalleryQueryDto? LastQuery { get; private set; }

        public Task<ServiceResult<FurniSpace.Application.DTOs.ProjectFiles.ProjectFileUploadResponseDto>> RegisterMeasurementImageAsync(
            Guid scheduleId,
            Guid currentUserId,
            RegisterMeasurementImageRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<FurniSpace.Application.DTOs.ProjectFiles.ProjectFileUploadResponseDto>.Unauthorized());

        public Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetProjectMeasurementImagesAsync(
            Guid projectId,
            Guid currentUserId,
            MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<MeasurementImageGalleryResponseDto>.Unauthorized());

        public Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetScheduleMeasurementImagesAsync(
            Guid scheduleId,
            Guid currentUserId,
            MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ServiceResult<MeasurementImageGalleryResponseDto>.Unauthorized());

        public Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetProjectAreaMeasurementImagesAsync(
            Guid projectAreaId,
            Guid currentUserId,
            MeasurementImageGalleryQueryDto query,
            CancellationToken cancellationToken = default)
        {
            LastProjectAreaId = projectAreaId;
            LastQuery = query;
            return Task.FromResult(_areaGalleryResult ?? ServiceResult<MeasurementImageGalleryResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<MeasurementImageAreaLinkResponseDto>> LinkMeasurementImageToAreaAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastProjectAreaId = projectAreaId;
            LastFileId = fileId;
            return Task.FromResult(_linkResult ?? ServiceResult<MeasurementImageAreaLinkResponseDto>.Unauthorized());
        }

        public Task<ServiceResult<MeasurementImageAreaLinkResponseDto>> UnlinkMeasurementImageFromAreaAsync(
            Guid projectAreaId,
            Guid fileId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            LastProjectAreaId = projectAreaId;
            LastFileId = fileId;
            return Task.FromResult(_unlinkResult ?? ServiceResult<MeasurementImageAreaLinkResponseDto>.Unauthorized());
        }
    }
}
