#nullable enable

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Notifications;
using FurniSpace.Application.Interfaces.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class NotificationsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthorization()
    {
        var authorize = typeof(NotificationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorize);
    }

    [Fact]
    public async Task GetMyNotifications_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        var controller = CreateController(new FakeNotificationService(), userId: null);

        var result = await controller.GetMyNotifications();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMyNotifications_PassesParametersToService()
    {
        var userId = Guid.NewGuid();
        var response = new NotificationListResponseDto { Page = 2, Limit = 10, Total = 5 };
        var service = new FakeNotificationService(
            listResult: ServiceResult<NotificationListResponseDto>.Success(response));
        var controller = CreateController(service, userId);

        var result = await controller.GetMyNotifications(isUnread: true, page: 2, limit: 10);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.LastUserId);
        Assert.True(service.LastIsUnread);
        Assert.Equal(2, service.LastPage);
        Assert.Equal(10, service.LastLimit);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        var controller = CreateController(new FakeNotificationService(), userId: null);

        var result = await controller.GetUnreadCount();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCountFromService()
    {
        var userId = Guid.NewGuid();
        var response = new NotificationUnreadCountDto { UnreadCount = 7 };
        var service = new FakeNotificationService(
            unreadCountResult: ServiceResult<NotificationUnreadCountDto>.Success(response));
        var controller = CreateController(service, userId);

        var result = await controller.GetUnreadCount();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var serviceResult = Assert.IsType<ServiceResult<NotificationUnreadCountDto>>(objectResult.Value);
        Assert.Equal(7, serviceResult.Data!.UnreadCount);
    }

    [Fact]
    public async Task MarkAsRead_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        var controller = CreateController(new FakeNotificationService(), userId: null);

        var result = await controller.MarkAsRead(Guid.NewGuid());

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task MarkAsRead_PassesNotificationIdAndUserIdToService()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var response = new MarkNotificationReadDto { NotificationId = notificationId, IsRead = true, ReadAt = DateTime.UtcNow };
        var service = new FakeNotificationService(
            markReadResult: ServiceResult<MarkNotificationReadDto>.Success(response, "Notification marked as read."));
        var controller = CreateController(service, userId);

        var result = await controller.MarkAsRead(notificationId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(notificationId, service.LastNotificationId);
        Assert.Equal(userId, service.LastUserId);
    }

    [Fact]
    public async Task MarkAllAsRead_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        var controller = CreateController(new FakeNotificationService(), userId: null);

        var result = await controller.MarkAllAsRead();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task MarkAllAsRead_CallsServiceWithCurrentUserId()
    {
        var userId = Guid.NewGuid();
        var service = new FakeNotificationService(
            markAllResult: ServiceResult<object>.Success(new { }, "All notifications marked as read."));
        var controller = CreateController(service, userId);

        var result = await controller.MarkAllAsRead();

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        Assert.Equal(userId, service.LastUserId);
    }

    private static NotificationsController CreateController(FakeNotificationService service, Guid? userId)
    {
        var controller = new NotificationsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUser(userId)
                }
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

    private sealed class FakeNotificationService : INotificationService
    {
        private readonly ServiceResult<NotificationListResponseDto> _listResult;
        private readonly ServiceResult<NotificationUnreadCountDto> _unreadCountResult;
        private readonly ServiceResult<MarkNotificationReadDto> _markReadResult;
        private readonly ServiceResult<object> _markAllResult;

        public FakeNotificationService(
            ServiceResult<NotificationListResponseDto>? listResult = null,
            ServiceResult<NotificationUnreadCountDto>? unreadCountResult = null,
            ServiceResult<MarkNotificationReadDto>? markReadResult = null,
            ServiceResult<object>? markAllResult = null)
        {
            _listResult = listResult ?? ServiceResult<NotificationListResponseDto>.Success(new NotificationListResponseDto());
            _unreadCountResult = unreadCountResult ?? ServiceResult<NotificationUnreadCountDto>.Success(new NotificationUnreadCountDto());
            _markReadResult = markReadResult ?? ServiceResult<MarkNotificationReadDto>.Success(new MarkNotificationReadDto());
            _markAllResult = markAllResult ?? ServiceResult<object>.Success(new { });
        }

        public Guid LastUserId { get; private set; }
        public Guid LastNotificationId { get; private set; }
        public bool? LastIsUnread { get; private set; }
        public int LastPage { get; private set; }
        public int LastLimit { get; private set; }

        public Task<ServiceResult<NotificationListResponseDto>> GetMyNotificationsAsync(
            Guid currentUserId, bool? isUnread, int page, int limit,
            CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            LastIsUnread = isUnread;
            LastPage = page;
            LastLimit = limit;
            return Task.FromResult(_listResult);
        }

        public Task<ServiceResult<NotificationUnreadCountDto>> GetUnreadCountAsync(
            Guid currentUserId, CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            return Task.FromResult(_unreadCountResult);
        }

        public Task<ServiceResult<MarkNotificationReadDto>> MarkAsReadAsync(
            Guid notificationId, Guid currentUserId, CancellationToken cancellationToken = default)
        {
            LastNotificationId = notificationId;
            LastUserId = currentUserId;
            return Task.FromResult(_markReadResult);
        }

        public Task<ServiceResult<object>> MarkAllAsReadAsync(
            Guid currentUserId, CancellationToken cancellationToken = default)
        {
            LastUserId = currentUserId;
            return Task.FromResult(_markAllResult);
        }
    }
}
