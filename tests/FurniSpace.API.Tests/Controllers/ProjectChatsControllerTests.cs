#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.ProjectChats;
using FurniSpace.Application.Interfaces.ProjectChats;
using FurniSpace.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class ProjectChatsControllerTests
{
    [Fact]
    public void Controller_UsesExpectedRouteAndRequiresAuthorization()
    {
        var route = typeof(ProjectChatsController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();
        var authorize = typeof(ProjectChatsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("projects/{projectId:guid}/chats", route.Template);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public void GetList_AllowsAllProjectParticipantRoles()
    {
        var authorize = typeof(ProjectChatsController)
            .GetMethod(nameof(ProjectChatsController.GetList))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("CUSTOMER,SALES,DESIGNER,ADMIN", authorize.Roles);
    }

    [Fact]
    public async Task GetList_ReturnsServiceResultAndPassesQuery()
    {
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var response = new ProjectChatListResponseDto
        {
            Items = [new ProjectChatListItemDto { ChatId = Guid.NewGuid(), ProjectId = projectId }],
            Page = 2,
            Limit = 10,
            Total = 1
        };
        var service = new FakeProjectChatService(
            ServiceResult<ProjectChatListResponseDto>.Success(
                response,
                "Project chats retrieved successfully."));
        var controller = BuildController(service, currentUserId);

        var actionResult = await controller.GetList(
            projectId,
            ProjectChatStatus.CLOSED,
            ProjectChatType.DESIGNER,
            page: 2,
            limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<ProjectChatListResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(projectId, service.ProjectId);
        Assert.Equal(currentUserId, service.CurrentUserId);
        Assert.NotNull(service.Query);
        Assert.Equal(ProjectChatStatus.CLOSED, service.Query.Status);
        Assert.Equal(ProjectChatType.DESIGNER, service.Query.ChatType);
        Assert.Equal(2, service.Query.Page);
        Assert.Equal(10, service.Query.Limit);
    }

    [Fact]
    public async Task GetList_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeProjectChatService(
            ServiceResult<ProjectChatListResponseDto>.Success(new ProjectChatListResponseDto()));
        var controller = BuildController(service);

        var actionResult = await controller.GetList(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(actionResult);
        Assert.Equal(0, service.GetListCallCount);
    }

    private static ProjectChatsController BuildController(
        IProjectChatService service,
        Guid? currentUserId = null)
    {
        var claims = currentUserId.HasValue
            ? new[] { new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString()) }
            : [];

        return new ProjectChatsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };
    }

    private sealed class FakeProjectChatService : IProjectChatService
    {
        private readonly ServiceResult<ProjectChatListResponseDto> _result;

        public FakeProjectChatService(ServiceResult<ProjectChatListResponseDto> result)
        {
            _result = result;
        }

        public int GetListCallCount { get; private set; }
        public Guid ProjectId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public ProjectChatListQueryDto? Query { get; private set; }

        public Task<ServiceResult<ProjectChatListResponseDto>> GetProjectChatsAsync(
            Guid projectId,
            Guid currentUserId,
            ProjectChatListQueryDto query,
            CancellationToken cancellationToken = default)
        {
            GetListCallCount++;
            ProjectId = projectId;
            CurrentUserId = currentUserId;
            Query = query;
            return Task.FromResult(_result);
        }

        public Task<ProjectChatSummaryDto> UpsertProjectChatAsync(
            Guid projectId,
            ProjectChatType chatType,
            Guid staffId,
            string title,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProjectChatSummaryDto());
        }
    }
}
