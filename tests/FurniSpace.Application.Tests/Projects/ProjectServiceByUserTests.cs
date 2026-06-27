#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.Projects;
using FurniSpace.Application.Services.Projects;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;

namespace FurniSpace.Application.Tests.Projects;

public sealed class ProjectServiceByUserTests
{
    [Fact]
    public async Task GetByUserAsync_WithCustomerOwnProjects_ReturnsPagedProjects()
    {
        var customerId = Guid.NewGuid();
        var item = CreateProjectByUserItem(customerId);
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?> { [customerId] = "CUSTOMER" },
            byUserItems: [item],
            byUserCount: 21);
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            customerId,
            customerId,
            new GetProjectsByUserQueryDto
            {
                Page = 2,
                PageSize = 10,
                Status = ProjectStatus.IN_CONSULTATION,
                Keyword = " cafe "
            });

        Assert.Equal(200, result.Status);
        Assert.Equal("Projects retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(21, result.Data.TotalItems);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.NotNull(repository.LastByUserQuery);
        Assert.Equal(customerId, repository.LastByUserQuery.UserId);
        Assert.Equal("CUSTOMER", repository.LastByUserQuery.RoleScope);
        Assert.Equal(ProjectStatus.IN_CONSULTATION, repository.LastByUserQuery.Status);
        Assert.Equal("cafe", repository.LastByUserQuery.Keyword);
        Assert.Equal(2, repository.LastByUserQuery.Page);
        Assert.Equal(10, repository.LastByUserQuery.PageSize);
        Assert.Equal(customerId, result.Data.Items[0].Customer.AccountId);
        Assert.Equal("Michael Chen", result.Data.Items[0].Customer.FullName);
    }

    [Fact]
    public async Task GetByUserAsync_WithAdminViewingDesignerProjects_AllowsDifferentUser()
    {
        var adminId = Guid.NewGuid();
        var designerId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?>
            {
                [adminId] = "ADMIN",
                [designerId] = "DESIGNER"
            },
            byUserItems: [CreateProjectByUserItem(Guid.NewGuid(), assignedDesignerId: designerId)],
            byUserCount: 1);
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            designerId,
            adminId,
            new GetProjectsByUserQueryDto { RoleScope = "designer" });

        Assert.Equal(200, result.Status);
        Assert.NotNull(repository.LastByUserQuery);
        Assert.Equal("DESIGNER", repository.LastByUserQuery.RoleScope);
    }

    [Fact]
    public async Task GetByUserAsync_WithMissingTargetUser_ReturnsUserNotFound()
    {
        var adminId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?> { [adminId] = "ADMIN" });
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            Guid.NewGuid(),
            adminId,
            new GetProjectsByUserQueryDto());

        Assert.Equal(404, result.Status);
        Assert.Equal("USER_NOT_FOUND", result.ErrorCode);
        Assert.Null(repository.LastByUserQuery);
    }

    [Fact]
    public async Task GetByUserAsync_WithCustomerAccessingAnotherUser_ReturnsForbidden()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?>
            {
                [requesterId] = "CUSTOMER",
                [targetId] = "CUSTOMER"
            });
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            targetId,
            requesterId,
            new GetProjectsByUserQueryDto());

        Assert.Equal(403, result.Status);
        Assert.Equal("You do not have access to view projects for this user.", result.Message);
        Assert.Null(repository.LastByUserQuery);
    }

    [Theory]
    [InlineData(0, 10, "Page must be greater than zero.")]
    [InlineData(1, 0, "Page size must be between 1 and 100.")]
    [InlineData(1, 101, "Page size must be between 1 and 100.")]
    public async Task GetByUserAsync_WithInvalidPagination_ReturnsBadRequest(
        int page,
        int pageSize,
        string expectedMessage)
    {
        var userId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?> { [userId] = "SALES" });
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            userId,
            userId,
            new GetProjectsByUserQueryDto { Page = page, PageSize = pageSize });

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(repository.LastByUserQuery);
    }

    [Fact]
    public async Task GetByUserAsync_WithAdminRoleScopeMismatch_ReturnsBadRequest()
    {
        var adminId = Guid.NewGuid();
        var salesId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?>
            {
                [adminId] = "ADMIN",
                [salesId] = "SALES"
            });
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            salesId,
            adminId,
            new GetProjectsByUserQueryDto { RoleScope = "DESIGNER" });

        Assert.Equal(400, result.Status);
        Assert.Equal("Role scope does not match the requested user's role.", result.Message);
        Assert.Null(repository.LastByUserQuery);
    }

    [Fact]
    public async Task GetByUserAsync_WithUnsupportedRoleScope_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var repository = new FakeProjectRepository(
            roles: new Dictionary<Guid, string?> { [userId] = "SALES" });
        var service = CreateService(repository);

        var result = await service.GetByUserAsync(
            userId,
            userId,
            new GetProjectsByUserQueryDto { RoleScope = "PRODUCTION" });

        Assert.Equal(400, result.Status);
        Assert.Equal("Role scope must be CUSTOMER, SALES, DESIGNER, or ADMIN.", result.Message);
        Assert.Null(repository.LastByUserQuery);
    }

    private static ProjectService CreateService(FakeProjectRepository repository)
    {
        return new ProjectService(repository, TestUnitOfWork.Instance);
    }

    private static ProjectByUserItemReadModel CreateProjectByUserItem(
        Guid customerId,
        Guid? assignedSalesId = null,
        Guid? assignedDesignerId = null)
    {
        return new ProjectByUserItemReadModel
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = "PRJ-2024-156",
            ProjectName = "Luxury Cafe Interior",
            BusinessType = "Cafe",
            ProjectAddress = "123 Main Street",
            TotalAreaSqm = 280,
            NumberOfFloors = 2,
            BudgetMin = 35000,
            BudgetMax = 88000,
            TargetCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30),
            Status = ProjectStatus.IN_CONSULTATION,
            Customer = new ProjectCustomerSummaryReadModel
            {
                AccountId = customerId,
                FullName = "Michael Chen",
                Email = "michael@example.com",
                Phone = "+1 555-0123"
            },
            AssignedSales = assignedSalesId.HasValue
                ? new ProjectAccountSummaryReadModel
                {
                    AccountId = assignedSalesId.Value,
                    FullName = "Sarah Johnson"
                }
                : null,
            AssignedDesigner = assignedDesignerId.HasValue
                ? new ProjectAccountSummaryReadModel
                {
                    AccountId = assignedDesignerId.Value,
                    FullName = "Emily Davis"
                }
                : null,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly IReadOnlyDictionary<Guid, string?> _roles;
        private readonly IReadOnlyList<ProjectByUserItemReadModel> _byUserItems;
        private readonly int _byUserCount;

        public FakeProjectRepository(
            IReadOnlyDictionary<Guid, string?> roles,
            IReadOnlyList<ProjectByUserItemReadModel>? byUserItems = null,
            int byUserCount = 0)
        {
            _roles = roles;
            _byUserItems = byUserItems ?? [];
            _byUserCount = byUserCount;
        }

        public ProjectByUserQueryReadModel? LastByUserQuery { get; private set; }

        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_roles.TryGetValue(accountId, out var roleName) ? roleName : null);
        }

        public Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
            ProjectByUserQueryReadModel query,
            CancellationToken cancellationToken = default)
        {
            LastByUserQuery = query;
            return Task.FromResult(_byUserItems);
        }

        public Task<int> CountByUserAsync(ProjectByUserQueryReadModel query, CancellationToken cancellationToken = default)
        {
            LastByUserQuery = query;
            return Task.FromResult(_byUserCount);
        }

        public Task<string?> GetAccountFullNameAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<int> CountSubmittedInYearAsync(int year, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<ProjectDetailReadModel?> GetDetailAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectDetailReadModel?>(null);
        public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DesignerAccountReadModel?>(null);
        public Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectListItemReadModel>>([]);
        public Task<int> CountAsync(ProjectListQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProjectSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProjectSearchIndexItemReadModel>>([]);
        public IQueryable<Project> Query() => Array.Empty<Project>().AsQueryable();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Project?>(null);
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Project>>([]);
        public Task AddAsync(Project entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<Project> entities, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public void Update(Project entity) { }
        public void Remove(Project entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
