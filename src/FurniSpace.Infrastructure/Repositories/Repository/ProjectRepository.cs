using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectRepository : GenericRepository<Project>, IProjectRepository
{
    private const string DesignerRoleName = "DESIGNER";

    public ProjectRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<string?> GetAccountRoleNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.AccountSet
            .Where(account => account.AccountId == accountId && account.DeletedAt == null)
            .Join(
                DbContext.RoleSet,
                account => account.RoleId,
                role => role.RoleId,
                (_, role) => role.RoleName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetAccountFullNameAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.AccountSet
            .Where(account => account.AccountId == accountId && account.DeletedAt == null)
            .Select(account => account.FullName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveAccountIdsByRoleNamesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoleNames = roleNames
            .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
            .Select(roleName => roleName.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedRoleNames.Count == 0)
        {
            return [];
        }

        return await DbContext.AccountSet
            .Where(account => account.DeletedAt == null && account.Status == AccountStatus.ACTIVE)
            .Join(
                DbContext.RoleSet,
                account => account.RoleId,
                role => role.RoleId,
                (account, role) => new { account, role })
            .Where(joined => normalizedRoleNames.Contains(joined.role.RoleName.ToUpper()))
            .Select(joined => joined.account.AccountId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountSubmittedInYearAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectSet.CountAsync(
            project =>
                (project.SubmittedAt.HasValue && project.SubmittedAt.Value.Year == year) ||
                (!project.SubmittedAt.HasValue && project.CreatedAt.HasValue && project.CreatedAt.Value.Year == year),
            cancellationToken);
    }

    public Task<ProjectDetailReadModel?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.ProjectSet
            .Where(project => project.ProjectId == projectId)
            .Select(project => new ProjectDetailReadModel
            {
                ProjectId = project.ProjectId,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                BusinessType = project.BusinessType,
                ProjectAddress = project.ProjectAddress,
                BusinessPurpose = project.BusinessPurpose,
                FurnitureRequirement = project.FurnitureRequirement,
                Description = project.Description,
                TotalAreaSqm = project.TotalAreaSqm,
                NumberOfFloors = project.NumberOfFloors,
                BudgetMin = project.BudgetMin,
                BudgetMax = project.BudgetMax,
                TargetCompletionDate = project.TargetCompletionDate,
                Status = project.Status,
                SubmittedAt = project.SubmittedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<DesignerAccountReadModel?> GetActiveDesignerAsync(
        Guid designerId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.AccountSet
            .Where(account =>
                account.AccountId == designerId &&
                account.DeletedAt == null &&
                account.Status == AccountStatus.ACTIVE)
            .Join(
                DbContext.RoleSet,
                account => account.RoleId,
                role => role.RoleId,
                (account, role) => new { account, role })
            .Where(joined => joined.role.RoleName == DesignerRoleName)
            .Select(joined => new DesignerAccountReadModel
            {
                AccountId = joined.account.AccountId,
                FullName = joined.account.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectListItemReadModel>> GetListAsync(
        ProjectListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await BuildProjectQueueQuery(query)
            .OrderByDescending(project => project.SubmittedAt)
            .ThenByDescending(project => project.ProjectId)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(project => new ProjectListItemReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                BusinessType = project.BusinessType,
                Status = project.Status,
                CustomerId = project.CustomerId,
                AssignedSalesId = project.AssignedSalesId,
                AssignedDesignerId = project.AssignedDesignerId,
                SubmittedAt = project.SubmittedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        ProjectListQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildProjectQueueQuery(query).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectByUserItemReadModel>> GetByUserAsync(
        ProjectByUserQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return await BuildProjectsByUserQuery(query)
            .OrderByDescending(project => project.SubmittedAt ?? project.CreatedAt)
            .ThenByDescending(project => project.ProjectId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(project => new ProjectByUserItemReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                BusinessType = project.BusinessType,
                ProjectAddress = project.ProjectAddress,
                TotalAreaSqm = project.TotalAreaSqm,
                NumberOfFloors = project.NumberOfFloors,
                BudgetMin = project.BudgetMin,
                BudgetMax = project.BudgetMax,
                TargetCompletionDate = project.TargetCompletionDate,
                Status = project.Status,
                Customer = DbContext.AccountSet
                    .Where(account => account.AccountId == project.CustomerId)
                    .Select(account => new ProjectCustomerSummaryReadModel
                    {
                        AccountId = account.AccountId,
                        FullName = account.FullName,
                        Email = account.Email,
                        Phone = account.Phone
                    })
                    .FirstOrDefault()!,
                AssignedSales = project.AssignedSalesId.HasValue
                    ? DbContext.AccountSet
                        .Where(account => account.AccountId == project.AssignedSalesId)
                        .Select(account => new ProjectAccountSummaryReadModel
                        {
                            AccountId = account.AccountId,
                            FullName = account.FullName
                        })
                        .FirstOrDefault()
                    : null,
                AssignedDesigner = project.AssignedDesignerId.HasValue
                    ? DbContext.AccountSet
                        .Where(account => account.AccountId == project.AssignedDesignerId)
                        .Select(account => new ProjectAccountSummaryReadModel
                        {
                            AccountId = account.AccountId,
                            FullName = account.FullName
                        })
                        .FirstOrDefault()
                    : null,
                SubmittedAt = project.SubmittedAt,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByUserAsync(
        ProjectByUserQueryReadModel query,
        CancellationToken cancellationToken = default)
    {
        return BuildProjectsByUserQuery(query).CountAsync(cancellationToken);
    }

    public Task<ProjectSearchIndexItemReadModel?> GetSearchIndexItemAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return BuildSearchIndexQuery()
            .Where(item => item.ProjectId == projectId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectSearchIndexItemReadModel>> GetSearchIndexPageAsync(
        int page,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await BuildSearchIndexQuery()
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.ProjectId)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ProjectSearchIndexItemReadModel> BuildSearchIndexQuery()
    {
        return DbContext.ProjectSet
            .Join(
                DbContext.AccountSet,
                project => project.CustomerId,
                account => account.AccountId,
                (project, account) => new { project, account })
            .Select(entry => new ProjectSearchIndexItemReadModel
            {
                ProjectId = entry.project.ProjectId,
                ProjectCode = entry.project.ProjectCode,
                ProjectName = entry.project.ProjectName,
                BusinessType = entry.project.BusinessType,
                Status = entry.project.Status,
                CustomerId = entry.project.CustomerId,
                CustomerName = entry.account.FullName,
                CustomerEmail = entry.account.Email,
                CustomerPhone = entry.account.Phone,
                AssignedSalesId = entry.project.AssignedSalesId,
                AssignedDesignerId = entry.project.AssignedDesignerId,
                SubmittedAt = entry.project.SubmittedAt
            });
    }

    private IQueryable<Project> BuildProjectQueueQuery(ProjectListQueryReadModel query)
    {
        var projects = DbContext.ProjectSet.AsQueryable();

        if (query.Status.HasValue)
        {
            projects = projects.Where(project => project.Status == query.Status);
        }

        if (query.AssignedSalesId.HasValue)
        {
            projects = projects.Where(project => project.AssignedSalesId == query.AssignedSalesId);
        }

        if (query.AssignedDesignerId.HasValue)
        {
            projects = projects.Where(project => project.AssignedDesignerId == query.AssignedDesignerId);
        }

        if (query.CustomerId.HasValue)
        {
            projects = projects.Where(project => project.CustomerId == query.CustomerId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            projects = projects.Where(project =>
                EF.Functions.ILike(project.ProjectCode ?? string.Empty, pattern) ||
                EF.Functions.ILike(project.ProjectName, pattern) ||
                DbContext.AccountSet.Any(account =>
                    account.AccountId == project.CustomerId &&
                    (
                        EF.Functions.ILike(account.FullName, pattern) ||
                        EF.Functions.ILike(account.Email, pattern) ||
                        EF.Functions.ILike(account.Phone ?? string.Empty, pattern)
                    )));
        }

        return projects;
    }

    private IQueryable<Project> BuildProjectsByUserQuery(ProjectByUserQueryReadModel query)
    {
        var projects = DbContext.ProjectSet.AsQueryable();

        projects = query.RoleScope.ToUpperInvariant() switch
        {
            "CUSTOMER" => projects.Where(project => project.CustomerId == query.UserId),
            "SALES" => projects.Where(project => project.AssignedSalesId == query.UserId),
            "DESIGNER" => projects.Where(project => project.AssignedDesignerId == query.UserId),
            "ADMIN" => projects,
            _ => projects.Where(project => false)
        };

        if (query.Status.HasValue)
        {
            projects = projects.Where(project => project.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var pattern = $"%{query.Keyword.Trim()}%";
            projects = projects.Where(project =>
                EF.Functions.ILike(project.ProjectCode ?? string.Empty, pattern) ||
                EF.Functions.ILike(project.ProjectName, pattern) ||
                EF.Functions.ILike(project.BusinessType ?? string.Empty, pattern) ||
                DbContext.AccountSet.Any(account =>
                    account.AccountId == project.CustomerId &&
                    (
                        EF.Functions.ILike(account.FullName, pattern) ||
                        EF.Functions.ILike(account.Email, pattern) ||
                        EF.Functions.ILike(account.Phone ?? string.Empty, pattern)
                    )));
        }

        return projects;
    }
}
