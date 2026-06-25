#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Accounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class AccountsControllerTests
{
    [Fact]
    public void GetAdminDetail_RequiresAdminRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(AccountsController.GetAdminDetail));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetAdminDetail_UsesAdminAccountRoute()
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(AccountsController.GetAdminDetail));

        var route = method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("/admin/accounts/{accountId:guid}", route.Template);
    }

    [Fact]
    public async Task GetAdminDetail_ReturnsServiceResultThroughBaseController()
    {
        var accountId = Guid.NewGuid();
        var response = new AccountDetailDto
        {
            AccountId = accountId,
            Email = "designer01@furnispace.com",
            FullName = "Emily Davis",
            Role = new AccountRoleDto
            {
                RoleId = Guid.NewGuid(),
                RoleName = "DESIGNER",
                Description = "Designer Staff"
            },
            Status = "ACTIVE"
        };
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(response, "Account detail retrieved successfully."));
        var controller = new AccountsController(service);

        var actionResult = await controller.GetAdminDetail(accountId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<AccountDetailDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Account detail retrieved successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(accountId, service.AdminDetailAccountId);
    }

    [Fact]
    public void GetAvailableDesigners_RequiresSalesOrAdminRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(AccountsController.GetAvailableDesigners));

        Assert.NotNull(authorize);
        Assert.Equal("SALES,ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetAvailableDesigners_UsesAvailableDesignersRoute()
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(AccountsController.GetAvailableDesigners));

        var route = method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("/accounts/designers/available", route.Template);
    }

    [Fact]
    public async Task GetAvailableDesigners_ReturnsServiceResultThroughBaseController()
    {
        var response = PagedResult<AvailableDesignerDto>.Create(
        [
            new AvailableDesignerDto
            {
                AccountId = Guid.NewGuid(),
                Email = "designer01@furnispace.com",
                FullName = "Emily Davis",
                Status = "ACTIVE",
                CurrentActiveProjectCount = 1,
                MaxActiveProjects = 2,
                AvailableSlot = 1
            }
        ], page: 2, pageSize: 5, totalItems: 6);
        var service = new FakeAccountService(
            adminDetailResult: ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            availableDesignersResult: ServiceResult<PagedResult<AvailableDesignerDto>>.Success(
                response,
                "Available designers retrieved successfully."));
        var controller = new AccountsController(service);

        var actionResult = await controller.GetAvailableDesigners(page: 2, pageSize: 5, search: "Emily");

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<PagedResult<AvailableDesignerDto>>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Available designers retrieved successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.NotNull(service.AvailableDesignerQuery);
        Assert.Equal(2, service.AvailableDesignerQuery.Page);
        Assert.Equal(5, service.AvailableDesignerQuery.PageSize);
        Assert.Equal("Emily", service.AvailableDesignerQuery.Search);
    }

    [Fact]
    public void UpdateMe_RequiresAuthenticatedUser()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(AccountsController.UpdateMe));

        Assert.NotNull(authorize);
        Assert.Null(authorize.Roles);
    }

    [Fact]
    public void UpdateMe_UsesAccountsMeRoute()
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(AccountsController.UpdateMe));

        var route = method.GetCustomAttributes(typeof(HttpPatchAttribute), inherit: false)
            .Cast<HttpPatchAttribute>()
            .Single();

        Assert.Equal("/accounts/me", route.Template);
    }

    [Fact]
    public async Task UpdateMe_ReturnsServiceResultThroughBaseController()
    {
        var accountId = Guid.NewGuid();
        var response = new MyProfileDto
        {
            AccountId = accountId,
            Email = "sales01@furnispace.com",
            FullName = "Sarah Johnson",
            Phone = "0900000001",
            AvatarUrl = "https://cdn.furnispace.com/avatars/sarah.png",
            Role = "SALES",
            Status = "ACTIVE",
            UpdatedAt = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc)
        };
        var service = new FakeAccountService(
            adminDetailResult: ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            updateProfileResult: ServiceResult<MyProfileDto>.Success(response, "Profile updated successfully."));
        var controller = CreateController(service, accountId);
        var request = new UpdateMyProfileRequestDto
        {
            FullName = "Sarah Johnson",
            Phone = "0900000001"
        };

        var actionResult = await controller.UpdateMe(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<MyProfileDto>>(objectResult.Value);
        Assert.Equal(200, result.Status);
        Assert.Equal("Profile updated successfully.", result.Message);
        Assert.Same(response, result.Data);
        Assert.Equal(accountId, service.CurrentUserId);
        Assert.Same(request, service.UpdateProfileRequest);
    }

    [Fact]
    public async Task UpdateMe_WithMissingUserClaim_ReturnsUnauthorizedServiceResult()
    {
        var service = new FakeAccountService(ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()));
        var controller = new AccountsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var actionResult = await controller.UpdateMe(new UpdateMyProfileRequestDto());

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(401, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult>(objectResult.Value);
        Assert.Equal("Unauthorized", result.Message);
    }

    private sealed class FakeAccountService : IAccountService
    {
        private readonly ServiceResult<AccountDetailDto> _adminDetailResult;
        private readonly ServiceResult<MyProfileDto> _updateProfileResult;
        private readonly ServiceResult<PagedResult<AvailableDesignerDto>> _availableDesignersResult;

        public FakeAccountService(
            ServiceResult<AccountDetailDto> adminDetailResult,
            ServiceResult<MyProfileDto>? updateProfileResult = null,
            ServiceResult<PagedResult<AvailableDesignerDto>>? availableDesignersResult = null)
        {
            _adminDetailResult = adminDetailResult;
            _updateProfileResult = updateProfileResult ?? ServiceResult<MyProfileDto>.Success(new MyProfileDto());
            _availableDesignersResult = availableDesignersResult ??
                ServiceResult<PagedResult<AvailableDesignerDto>>.Success(
                    PagedResult<AvailableDesignerDto>.Create([], page: 1, pageSize: 10, totalItems: 0));
        }

        public Guid AdminDetailAccountId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public UpdateMyProfileRequestDto? UpdateProfileRequest { get; private set; }
        public AvailableDesignerQueryDto? AvailableDesignerQuery { get; private set; }

        public Task<ServiceResult<AccountDetailDto>> GetAdminDetailAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            AdminDetailAccountId = accountId;
            return Task.FromResult(_adminDetailResult);
        }

        public Task<ServiceResult<MyProfileDto>> UpdateMyProfileAsync(
            Guid currentUserId,
            UpdateMyProfileRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CurrentUserId = currentUserId;
            UpdateProfileRequest = request;
            return Task.FromResult(_updateProfileResult);
        }

        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetAvailableDesignersAsync(
            AvailableDesignerQueryDto query,
            CancellationToken cancellationToken = default)
        {
            AvailableDesignerQuery = query;
            return Task.FromResult(_availableDesignersResult);
        }

        public Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<AccountDto>.Created(new AccountDto()));
        }

        public Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<AccountDto>.Success(new AccountDto()));
        }

        public Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(
            int page,
            int pageSize,
            string? search,
            string? status,
            bool includeDeleted,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<PagedResult<AccountDto>>.Success(
                PagedResult<AccountDto>.Create([], page, pageSize, totalItems: 0)));
        }

        public Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(
            bool includeDeleted,
            CancellationToken cancellationToken = default)
        {
            _ = includeDeleted;
            return Task.FromResult(ServiceResult<AccountSearchStatsDto>.Success(
                new AccountSearchStatsDto(),
                "Account search stats retrieved successfully."));
        }

        public Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            _ = query;
            _ = limit;
            return Task.FromResult(ServiceResult<AccountSuggestResponseDto>.Success(
                new AccountSuggestResponseDto(),
                string.Empty));
        }

        public Task<ServiceResult<AccountDto>> UpdateAsync(
            Guid accountId,
            UpdateAccountRequestDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult<AccountDto>.Success(new AccountDto()));
        }

        public Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ServiceResult.Success());
        }
    }

    private static AuthorizeAttribute? GetMethodAuthorizeAttribute(string methodName)
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == methodName);

        return method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
    }

    private static AccountsController CreateController(FakeAccountService service, Guid accountId)
    {
        return new AccountsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, accountId.ToString())
                    ], "Test"))
                }
            }
        };
    }
}
