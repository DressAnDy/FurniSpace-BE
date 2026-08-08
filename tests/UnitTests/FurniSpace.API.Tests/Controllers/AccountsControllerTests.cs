#nullable enable

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
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
    [Theory]
    [InlineData(nameof(AccountsController.Suggest), "ADMIN")]
    [InlineData(nameof(AccountsController.GetSearchStats), "ADMIN")]
    public void AdminSearchActions_RequireAdminRole(string methodName, string expectedRoles)
    {
        var authorize = GetMethodAuthorizeAttribute(methodName);

        Assert.NotNull(authorize);
        Assert.Equal(expectedRoles, authorize.Roles);
    }

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
    public async Task GetPaged_ReturnsServiceResultAndPassesQuery()
    {
        var page = PagedResult<AccountDto>.Create(
        [
            new AccountDto
            {
                AccountId = Guid.NewGuid(),
                Email = "admin@furnispace.com",
                FullName = "Admin User",
                Status = "ACTIVE"
            }
        ], page: 3, pageSize: 15, totalItems: 31);
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            pagedResult: ServiceResult<PagedResult<AccountDto>>.Success(page, "Accounts retrieved successfully."));
        var controller = new AccountsController(service);

        var actionResult = await controller.GetPaged(
            page: 3,
            pageSize: 15,
            search: "admin",
            status: "ACTIVE",
            includeDeleted: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<PagedResult<AccountDto>>>(objectResult.Value);
        Assert.Same(page, result.Data);
        Assert.Equal(3, service.Page);
        Assert.Equal(15, service.PageSize);
        Assert.Equal("admin", service.Search);
        Assert.Equal("ACTIVE", service.Status);
        Assert.True(service.IncludeDeleted);
    }

    [Fact]
    public async Task GetById_ReturnsServiceResultAndPassesAccountId()
    {
        var accountId = Guid.NewGuid();
        var response = new AccountDto
        {
            AccountId = accountId,
            Email = "designer@furnispace.com",
            FullName = "Designer User"
        };
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            getByIdResult: ServiceResult<AccountDto>.Success(response, "Account retrieved successfully."));
        var controller = new AccountsController(service);

        var actionResult = await controller.GetById(accountId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<AccountDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(accountId, service.AccountId);
    }

    [Fact]
    public async Task Suggest_ReturnsServiceResultAndPassesQuery()
    {
        var response = new AccountSuggestResponseDto
        {
            Items =
            [
                new AccountSuggestItemDto
                {
                    AccountId = Guid.NewGuid(),
                    FullName = "Sarah Johnson",
                    Email = "sarah@furnispace.com"
                }
            ]
        };
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            suggestResult: ServiceResult<AccountSuggestResponseDto>.Success(response, string.Empty));
        var controller = new AccountsController(service);

        var actionResult = await controller.Suggest("sarah", limit: 5);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<AccountSuggestResponseDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal("sarah", service.SuggestQuery);
        Assert.Equal(5, service.SuggestLimit);
    }

    [Fact]
    public async Task GetSearchStats_ReturnsServiceResultAndPassesIncludeDeleted()
    {
        var response = new AccountSearchStatsDto();
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            searchStatsResult: ServiceResult<AccountSearchStatsDto>.Success(
                response,
                "Account search stats retrieved successfully."));
        var controller = new AccountsController(service);

        var actionResult = await controller.GetSearchStats(includeDeleted: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<AccountSearchStatsDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.True(service.SearchStatsIncludeDeleted);
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
    public void GetDesignerWorkload_RequiresAdminRole()
    {
        var authorize = GetMethodAuthorizeAttribute(nameof(AccountsController.GetDesignerWorkload));

        Assert.NotNull(authorize);
        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetDesignerWorkload_UsesAdminWorkloadRoute()
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(AccountsController.GetDesignerWorkload));

        var route = method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("/admin/designers/workload", route.Template);
    }

    [Fact]
    public void GetDesignerWorkloadSummary_UsesSummaryRoute()
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(AccountsController.GetDesignerWorkloadSummary));

        var route = method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("/admin/designers/workload/summary", route.Template);
    }

    [Fact]
    public void GetDesignerAssignedProjects_UsesDesignerProjectsRoute()
    {
        var method = typeof(AccountsController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(AccountsController.GetDesignerAssignedProjects));

        var route = method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("/admin/designers/{designerId:guid}/projects", route.Template);
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

    [Fact]
    public async Task Create_ReturnsServiceResultAndPassesRequest()
    {
        var response = new AccountDto
        {
            AccountId = Guid.NewGuid(),
            Email = "new@furnispace.com",
            FullName = "New User"
        };
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            createResult: ServiceResult<AccountDto>.Created(response, "Account created successfully."));
        var controller = new AccountsController(service);
        var request = new CreateAccountRequestDto
        {
            Email = "new@furnispace.com",
            FullName = "New User",
            Password = "Secure123"
        };

        var actionResult = await controller.Create(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(201, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<AccountDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Same(request, service.CreateRequest);
    }

    [Fact]
    public async Task Update_ReturnsServiceResultAndPassesRequest()
    {
        var accountId = Guid.NewGuid();
        var response = new AccountDto
        {
            AccountId = accountId,
            Email = "updated@furnispace.com",
            FullName = "Updated User"
        };
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            updateResult: ServiceResult<AccountDto>.Success(response, "Account updated successfully."));
        var controller = new AccountsController(service);
        var request = new UpdateAccountRequestDto
        {
            Email = "updated@furnispace.com",
            FullName = "Updated User"
        };

        var actionResult = await controller.Update(accountId, request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<AccountDto>>(objectResult.Value);
        Assert.Same(response, result.Data);
        Assert.Equal(accountId, service.AccountId);
        Assert.Same(request, service.UpdateRequest);
    }

    [Fact]
    public async Task Delete_ReturnsServiceResultAndPassesAccountId()
    {
        var accountId = Guid.NewGuid();
        var service = new FakeAccountService(
            ServiceResult<AccountDetailDto>.Success(new AccountDetailDto()),
            deleteResult: ServiceResult.Success("Account deleted successfully."));
        var controller = new AccountsController(service);

        var actionResult = await controller.Delete(accountId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult>(objectResult.Value);
        Assert.Equal("Account deleted successfully.", result.Message);
        Assert.Equal(accountId, service.AccountId);
    }

    private sealed class FakeAccountService : IAccountService
    {
        private readonly ServiceResult<AccountDetailDto> _adminDetailResult;
        private readonly ServiceResult<MyProfileDto> _updateProfileResult;
        private readonly ServiceResult<PagedResult<AvailableDesignerDto>> _availableDesignersResult;
        private readonly ServiceResult<AccountDto> _createResult;
        private readonly ServiceResult<AccountDto> _getByIdResult;
        private readonly ServiceResult<PagedResult<AccountDto>> _pagedResult;
        private readonly ServiceResult<AccountSearchStatsDto> _searchStatsResult;
        private readonly ServiceResult<AccountSuggestResponseDto> _suggestResult;
        private readonly ServiceResult<AccountDto> _updateResult;
        private readonly ServiceResult _deleteResult;

        public FakeAccountService(
            ServiceResult<AccountDetailDto> adminDetailResult,
            ServiceResult<MyProfileDto>? updateProfileResult = null,
            ServiceResult<PagedResult<AvailableDesignerDto>>? availableDesignersResult = null,
            ServiceResult<AccountDto>? createResult = null,
            ServiceResult<AccountDto>? getByIdResult = null,
            ServiceResult<PagedResult<AccountDto>>? pagedResult = null,
            ServiceResult<AccountSearchStatsDto>? searchStatsResult = null,
            ServiceResult<AccountSuggestResponseDto>? suggestResult = null,
            ServiceResult<AccountDto>? updateResult = null,
            ServiceResult? deleteResult = null)
        {
            _adminDetailResult = adminDetailResult;
            _updateProfileResult = updateProfileResult ?? ServiceResult<MyProfileDto>.Success(new MyProfileDto());
            _availableDesignersResult = availableDesignersResult ??
                ServiceResult<PagedResult<AvailableDesignerDto>>.Success(
                    PagedResult<AvailableDesignerDto>.Create([], page: 1, pageSize: 10, totalItems: 0));
            _createResult = createResult ?? ServiceResult<AccountDto>.Created(new AccountDto());
            _getByIdResult = getByIdResult ?? ServiceResult<AccountDto>.Success(new AccountDto());
            _pagedResult = pagedResult ?? ServiceResult<PagedResult<AccountDto>>.Success(
                PagedResult<AccountDto>.Create([], page: 1, pageSize: 20, totalItems: 0));
            _searchStatsResult = searchStatsResult ?? ServiceResult<AccountSearchStatsDto>.Success(
                new AccountSearchStatsDto(),
                "Account search stats retrieved successfully.");
            _suggestResult = suggestResult ?? ServiceResult<AccountSuggestResponseDto>.Success(
                new AccountSuggestResponseDto(),
                string.Empty);
            _updateResult = updateResult ?? ServiceResult<AccountDto>.Success(new AccountDto());
            _deleteResult = deleteResult ?? ServiceResult.Success();
        }

        public Guid AdminDetailAccountId { get; private set; }
        public Guid AccountId { get; private set; }
        public Guid CurrentUserId { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }
        public string? Search { get; private set; }
        public string? Status { get; private set; }
        public bool IncludeDeleted { get; private set; }
        public bool SearchStatsIncludeDeleted { get; private set; }
        public string? SuggestQuery { get; private set; }
        public int SuggestLimit { get; private set; }
        public CreateAccountRequestDto? CreateRequest { get; private set; }
        public UpdateAccountRequestDto? UpdateRequest { get; private set; }
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

        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetDesignerWorkloadAsync(
            DesignerWorkloadQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<PagedResult<AvailableDesignerDto>>.Success(
                PagedResult<AvailableDesignerDto>.Create([], 1, 20, 0)));

        public Task<ServiceResult<DesignerWorkloadSummaryDto>> GetDesignerWorkloadSummaryAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<DesignerWorkloadSummaryDto>.Success(new DesignerWorkloadSummaryDto()));

        public Task<ServiceResult<PagedResult<DesignerAssignedProjectDto>>> GetDesignerAssignedProjectsAsync(
            Guid designerId,
            DesignerAssignedProjectQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<PagedResult<DesignerAssignedProjectDto>>.Success(
                PagedResult<DesignerAssignedProjectDto>.Create([], 1, 20, 0)));

        public Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default)
        {
            CreateRequest = request;
            return Task.FromResult(_createResult);
        }

        public Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            AccountId = accountId;
            return Task.FromResult(_getByIdResult);
        }

        public Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(
            int page,
            int pageSize,
            string? search,
            string? status,
            bool includeDeleted,
            CancellationToken cancellationToken = default)
        {
            Page = page;
            PageSize = pageSize;
            Search = search;
            Status = status;
            IncludeDeleted = includeDeleted;
            return Task.FromResult(_pagedResult);
        }

        public Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(
            bool includeDeleted,
            CancellationToken cancellationToken = default)
        {
            SearchStatsIncludeDeleted = includeDeleted;
            return Task.FromResult(_searchStatsResult);
        }

        public Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            SuggestQuery = query;
            SuggestLimit = limit;
            return Task.FromResult(_suggestResult);
        }

        public Task<ServiceResult<AccountDto>> UpdateAsync(
            Guid accountId,
            UpdateAccountRequestDto request,
            CancellationToken cancellationToken = default)
        {
            AccountId = accountId;
            UpdateRequest = request;
            return Task.FromResult(_updateResult);
        }

        public Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            AccountId = accountId;
            return Task.FromResult(_deleteResult);
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
