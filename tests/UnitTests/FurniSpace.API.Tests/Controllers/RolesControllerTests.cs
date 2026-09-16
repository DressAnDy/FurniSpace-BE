#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.API.Controllers.Admin;
using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.Accounts;
using FurniSpace.Application.Interfaces.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurniSpace.API.Tests.Controllers;

public sealed class RolesControllerTests
{
    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var authorize = typeof(RolesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("ADMIN", authorize.Roles);
    }

    [Fact]
    public void GetAll_UsesAdminRolesRoute()
    {
        var method = typeof(RolesController)
            .GetMethods()
            .Single(methodInfo => methodInfo.Name == nameof(RolesController.GetAll));

        var route = method.DeclaringType!
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();

        Assert.Equal("admin/roles", route.Template);
    }

    [Fact]
    public async Task GetAll_ReturnsServiceResultThroughBaseController()
    {
        var roles = new[]
        {
            new AccountRoleDto { RoleId = System.Guid.NewGuid(), RoleName = "ADMIN", Description = "Admin" }
        };
        var service = new FakeRolesAccountService(
            ServiceResult<IReadOnlyList<AccountRoleDto>>.Success(roles, "Roles retrieved successfully."));
        var controller = new RolesController(service);

        var actionResult = await controller.GetAll(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);
        var result = Assert.IsType<ServiceResult<IReadOnlyList<AccountRoleDto>>>(objectResult.Value);
        Assert.Same(roles, result.Data);
    }

    private sealed class FakeRolesAccountService : IAccountService
    {
        private readonly ServiceResult<IReadOnlyList<AccountRoleDto>> _allRolesResult;

        public FakeRolesAccountService(ServiceResult<IReadOnlyList<AccountRoleDto>> allRolesResult)
        {
            _allRolesResult = allRolesResult;
        }

        public Task<ServiceResult<IReadOnlyList<AccountRoleDto>>> GetAllRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_allRolesResult);

        public Task<ServiceResult<AccountDto>> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<AccountDto>> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<AccountDetailDto>> GetAdminDetailAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<MyProfileDto>> UpdateMyProfileAsync(Guid currentUserId, UpdateMyProfileRequestDto request, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetAvailableDesignersAsync(AvailableDesignerQueryDto query, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<AvailableDesignerDto>>> GetDesignerWorkloadAsync(DesignerWorkloadQueryDto query, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<DesignerWorkloadSummaryDto>> GetDesignerWorkloadSummaryAsync(CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<DesignerAssignedProjectDto>>> GetDesignerAssignedProjectsAsync(Guid designerId, DesignerAssignedProjectQueryDto query, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<SalesWorkloadItemDto>>> GetSalesWorkloadAsync(SalesWorkloadQueryDto query, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<SalesWorkloadSummaryDto>> GetSalesWorkloadSummaryAsync(CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<SalesAssignedProjectDto>>> GetSalesAssignedProjectsAsync(Guid salesId, SalesAssignedProjectQueryDto query, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<UnassignedIntakeProjectDto>>> GetUnassignedIntakeProjectsAsync(UnassignedIntakeProjectQueryDto query, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<PagedResult<AccountDto>>> GetPagedAsync(int page, int pageSize, string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<AccountSearchStatsDto>> GetSearchStatsAsync(bool includeDeleted, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<AccountSuggestResponseDto>> SuggestAsync(string query, int limit, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult<AccountDto>> UpdateAsync(Guid accountId, UpdateAccountRequestDto request, Guid currentUserId, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<ServiceResult> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
    }
}
