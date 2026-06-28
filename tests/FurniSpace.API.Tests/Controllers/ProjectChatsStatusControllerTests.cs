#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Chat;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectChatsStatusControllerTests
{
    [Fact]
    public void Controller_UsesExpectedRouteAndStaffRoles()
    {
        var route = typeof(ProjectChatsStatusController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();
        var authorize = typeof(ProjectChatsStatusController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();
        var httpPatch = typeof(ProjectChatsStatusController)
            .GetMethod(nameof(ProjectChatsStatusController.UpdateStatus))!
            .GetCustomAttributes(typeof(HttpPatchAttribute), inherit: false)
            .Cast<HttpPatchAttribute>()
            .Single();

        Assert.Equal("project-chats/{chatId:guid}/status", route.Template);
        Assert.Equal("SALES,DESIGNER,ADMIN", authorize.Roles);
        Assert.Null(httpPatch.Template);
    }

    [Fact]
    public async Task UpdateStatus_ReturnsServiceResultAndPassesRequest()
    {
        var chatId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var request = new UpdateProjectChatStatusRequestDto { Status = ProjectChatStatus.CLOSED };
        var response = new ProjectChatSummaryDto
        {
            ChatId = chatId,
            ProjectId = Guid.NewGuid(),
            ChatType = ProjectChatType.SALES.ToString(),
            Status = ProjectChatStatus.CLOSED.ToString(),
            ClosedAt = DateTime.UtcNow
        };
        var service = new FakeProjectChatService(
            ServiceResult<ProjectChatSummaryDto>.Success(
                response,
                "Project chat closed successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.UpdateStatus(chatId, request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectChatSummaryDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(chatId, service.LastChatId);
        Assert.Equal(currentUserId, service.LastCurrentUserId);
        Assert.Same(request, service.LastStatusRequest);
    }

    [Fact]
    public async Task UpdateStatus_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectChatService(
            ServiceResult<ProjectChatSummaryDto>.Success(new ProjectChatSummaryDto()));
        var controller = new ProjectChatsStatusController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var actionResult = await controller.UpdateStatus(
            Guid.NewGuid(),
            new UpdateProjectChatStatusRequestDto { Status = ProjectChatStatus.CLOSED });

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(Guid.Empty, service.LastCurrentUserId);
    }

    private static ProjectChatsStatusController BuildController(
        IProjectChatService service,
        Guid currentUserId)
    {
        var controller = new ProjectChatsStatusController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString())
                    ]))
                }
            }
        };

        return controller;
    }

    private sealed class FakeProjectChatService : IProjectChatService
    {
        private readonly ServiceResult<ProjectChatSummaryDto>? _updateStatusResult;

        public FakeProjectChatService(ServiceResult<ProjectChatSummaryDto> updateStatusResult)
        {
            _updateStatusResult = updateStatusResult;
        }

        public Guid LastChatId { get; private set; }
        public Guid LastCurrentUserId { get; private set; }
        public UpdateProjectChatStatusRequestDto? LastStatusRequest { get; private set; }

        public Task<bool> CanAccessProjectAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult<ProjectChatSummaryDto>> CreateManualAsync(
            Guid projectId,
            Guid currentUserId,
            CreateProjectChatRequestDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult<ProjectChatListResponseDto>> GetProjectChatsAsync(
            Guid projectId,
            Guid currentUserId,
            ProjectChatListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProjectChatSummaryDto> UpsertProjectChatAsync(
            Guid projectId,
            ProjectChatType chatType,
            Guid staffId,
            string title,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult<ProjectChatSummaryDto>> UpdateStatusAsync(
            Guid chatId,
            Guid currentUserId,
            UpdateProjectChatStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            LastChatId = chatId;
            LastCurrentUserId = currentUserId;
            LastStatusRequest = request;
            return Task.FromResult(_updateStatusResult!);
        }
    }
}
