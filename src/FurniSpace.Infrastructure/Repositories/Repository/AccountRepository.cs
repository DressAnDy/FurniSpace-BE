using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Accounts;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Accounts;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class AccountRepository : GenericRepository<Account>, IAccountRepository
{
    private const string DesignerRoleName = "DESIGNER";
    private const string SalesRoleName = "SALES";
    private const string SortAvailableSlotDesc = "AvailableSlotDesc";
    private const string SortSalesActiveCountDesc = "SalesActiveCountDesc";
    private const string SortAvailableSlotAsc = "AvailableSlotAsc";

    public AccountRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(account => account.Email == email, cancellationToken);
    }

    public Task<AccountDetailReadModel?> GetDetailAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return (
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == accountId
            select new AccountDetailReadModel
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Phone = account.Phone,
                AvatarUrl = account.AvatarUrl,
                Role = new AccountRoleReadModel
                {
                    RoleId = role.RoleId,
                    RoleName = role.RoleName,
                    Description = role.Description
                },
                Status = account.Status,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                DeletedAt = account.DeletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetRoleNameAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return DbContext.RoleSet
            .Where(role => role.RoleId == roleId)
            .Select(role => role.RoleName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Guid?> GetRoleIdByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return DbContext.RoleSet
            .Where(role => role.RoleName == roleName)
            .Select(role => (Guid?)role.RoleId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return DbContext.RoleSet.AnyAsync(role => role.RoleId == roleId, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludedAccountId = null, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(
            account => account.Email == email &&
                (!excludedAccountId.HasValue || account.AccountId != excludedAccountId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableDesignerReadModel>> GetAvailableDesignersAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        CancellationToken cancellationToken = default)
    {
        return await BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState: null)
            .OrderBy(designer => designer.DesignActiveCount)
            .ThenBy(designer => designer.FullName)
            .ThenBy(designer => designer.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAvailableDesignersAsync(
        int maxActiveProjects,
        string? search,
        CancellationToken cancellationToken = default)
    {
        return BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState: null)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableDesignerReadModel>> GetDesignerWorkloadAsync(
        int page,
        int pageSize,
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string sortBy,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState);
        query = ApplyWorkloadSort(query, sortBy);

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountDesignerWorkloadAsync(
        int maxActiveProjects,
        string? search,
        string? capacityState,
        CancellationToken cancellationToken = default)
    {
        return BuildDesignerWorkloadQuery(maxActiveProjects, search, capacityState)
            .CountAsync(cancellationToken);
    }

    public async Task<DesignerWorkloadSummaryReadModel> GetDesignerWorkloadSummaryAsync(
        int maxActiveProjects,
        CancellationToken cancellationToken = default)
    {
        var designers = BuildDesignerWorkloadQuery(maxActiveProjects, search: null, capacityState: null);

        var summary = await designers
            .GroupBy(_ => 1)
            .Select(group => new DesignerWorkloadSummaryReadModel
            {
                TotalActiveDesigners = group.Count(),
                AvailableCount = group.Count(designer => designer.CapacityState == DesignerWorkloadStatusSets.CapacityAvailable),
                FullCount = group.Count(designer => designer.CapacityState == DesignerWorkloadStatusSets.CapacityFull),
                OverCount = group.Count(designer => designer.CapacityState == DesignerWorkloadStatusSets.CapacityOver),
                TotalDesignActiveProjects = group.Sum(designer => designer.DesignActiveCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return summary ?? new DesignerWorkloadSummaryReadModel();
    }

    public async Task<IReadOnlyList<DesignerAssignedProjectReadModel>> GetDesignerAssignedProjectsAsync(
        Guid designerId,
        int page,
        int pageSize,
        string? bucket,
        CancellationToken cancellationToken = default)
    {
        return await BuildDesignerAssignedProjectsQuery(designerId, bucket)
            .OrderByDescending(project => project.DesignerAssignedAt)
            .ThenByDescending(project => project.ProjectCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountDesignerAssignedProjectsAsync(
        Guid designerId,
        string? bucket,
        CancellationToken cancellationToken = default)
    {
        return BuildDesignerAssignedProjectsQuery(designerId, bucket).CountAsync(cancellationToken);
    }

    public Task<bool> IsActiveDesignerAsync(Guid designerId, CancellationToken cancellationToken = default)
    {
        return (
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == designerId &&
                role.RoleName == DesignerRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            select account.AccountId)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesWorkloadItemReadModel>> GetSalesWorkloadAsync(
        SalesWorkloadListQuery query,
        CancellationToken cancellationToken = default)
    {
        var workload = BuildSalesWorkloadQuery(
            query.MaxActiveProjects,
            query.Search,
            query.CapacityState,
            query.FuturePressureState);
        workload = ApplySalesWorkloadSort(workload, query.SortBy);

        return await workload
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountSalesWorkloadAsync(
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string? futurePressureState,
        CancellationToken cancellationToken = default)
    {
        return BuildSalesWorkloadQuery(maxActiveProjects, search, capacityState, futurePressureState)
            .CountAsync(cancellationToken);
    }

    public async Task<SalesWorkloadSummaryReadModel> GetSalesWorkloadSummaryAsync(
        int maxActiveProjects,
        CancellationToken cancellationToken = default)
    {
        var sales = BuildSalesWorkloadQuery(maxActiveProjects, search: null, capacityState: null, futurePressureState: null);
        var summary = await sales
            .GroupBy(_ => 1)
            .Select(group => new SalesWorkloadSummaryReadModel
            {
                TotalActiveSales = group.Count(),
                AvailableNowCount = group.Count(item => item.CapacityState == SalesWorkloadPressurePolicy.CapacityAvailableNow),
                FullNowCount = group.Count(item => item.CapacityState == SalesWorkloadPressurePolicy.CapacityFullNow),
                OverNowCount = group.Count(item => item.CapacityState == SalesWorkloadPressurePolicy.CapacityOverNow),
                HighFuturePressureCount = group.Count(item => item.FuturePressureState == SalesWorkloadPressurePolicy.PressureHigh),
                TotalSalesActiveProjects = group.Sum(item => item.SalesActiveCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        summary ??= new SalesWorkloadSummaryReadModel();
        summary.UnassignedIntakeCount = await CountUnassignedIntakeProjectsAsync(cancellationToken);
        return summary;
    }

    public async Task<IReadOnlyList<SalesAssignedProjectReadModel>> GetSalesAssignedProjectsAsync(
        Guid salesId,
        int page,
        int pageSize,
        string? bucket,
        CancellationToken cancellationToken = default)
    {
        return await BuildSalesAssignedProjectsQuery(salesId, bucket)
            .OrderByDescending(project => project.SalesAssignedAt)
            .ThenByDescending(project => project.ProjectCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountSalesAssignedProjectsAsync(
        Guid salesId,
        string? bucket,
        CancellationToken cancellationToken = default)
    {
        return BuildSalesAssignedProjectsQuery(salesId, bucket).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnassignedIntakeProjectReadModel>> GetUnassignedIntakeProjectsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await BuildUnassignedIntakeQuery()
            .OrderByDescending(project => project.SubmittedAt)
            .ThenByDescending(project => project.ProjectCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnassignedIntakeProjectsAsync(CancellationToken cancellationToken = default)
    {
        return BuildUnassignedIntakeQuery().CountAsync(cancellationToken);
    }

    public Task<bool> IsActiveSalesAsync(Guid salesId, CancellationToken cancellationToken = default)
    {
        return (
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where account.AccountId == salesId &&
                role.RoleName == SalesRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            select account.AccountId)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search, status, includeDeleted)
            .OrderByDescending(account => account.CreatedAt)
            .ThenBy(account => account.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(string? search, string? status, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        return BuildQuery(search, status, includeDeleted).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByStatusAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search: null, status: null, includeDeleted)
            .GroupBy(account => account.Status)
            .Select(group => new AccountFacetCountReadModel
            {
                Key = group.Key == null ? "UNKNOWN" : group.Key.ToString()!,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountFacetCountReadModel>> CountGroupedByRoleIdAsync(
        bool includeDeleted,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(search: null, status: null, includeDeleted)
            .GroupBy(account => account.RoleId)
            .Select(group => new AccountFacetCountReadModel
            {
                Key = group.Key.ToString(),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Account> BuildQuery(string? search, string? status, bool includeDeleted)
    {
        var query = Query();

        if (!includeDeleted)
        {
            query = query.Where(account => account.DeletedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = BuildSearchPattern(search);
            query = query.Where(account =>
                EF.Functions.ILike(account.Email, pattern) ||
                EF.Functions.ILike(account.FullName, pattern) ||
                (account.Phone != null && EF.Functions.ILike(account.Phone, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var accountStatus))
        {
            query = query.Where(account => account.Status == accountStatus);
        }

        return query;
    }

    private IQueryable<AvailableDesignerReadModel> BuildDesignerWorkloadQuery(
        int maxActiveProjects,
        string? search,
        string? capacityState)
    {
        var designActiveStatuses = DesignerWorkloadStatusSets.DesignActive;
        var lifecycleStatuses = DesignerWorkloadStatusSets.LifecycleAssigned;

        var query =
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where role.RoleName == DesignerRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            let designActiveCount = DbContext.ProjectSet.Count(project =>
                project.AssignedDesignerId == account.AccountId &&
                project.Status.HasValue &&
                designActiveStatuses.Contains(project.Status.Value))
            let lifecycleAssignedCount = DbContext.ProjectSet.Count(project =>
                project.AssignedDesignerId == account.AccountId &&
                project.Status.HasValue &&
                lifecycleStatuses.Contains(project.Status.Value))
            select new AvailableDesignerReadModel
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Phone = account.Phone,
                AvatarUrl = account.AvatarUrl,
                Status = account.Status,
                DesignActiveCount = designActiveCount,
                LifecycleAssignedCount = lifecycleAssignedCount,
                CurrentActiveProjectCount = designActiveCount,
                MaxActiveProjects = maxActiveProjects,
                AvailableSlot = maxActiveProjects - designActiveCount,
                CapacityState = designActiveCount < maxActiveProjects
                    ? DesignerWorkloadStatusSets.CapacityAvailable
                    : designActiveCount == maxActiveProjects
                        ? DesignerWorkloadStatusSets.CapacityFull
                        : DesignerWorkloadStatusSets.CapacityOver,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = BuildSearchPattern(search);
            query = query.Where(designer =>
                EF.Functions.ILike(designer.Email, pattern) ||
                EF.Functions.ILike(designer.FullName, pattern) ||
                (designer.Phone != null && EF.Functions.ILike(designer.Phone, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(capacityState))
        {
            var normalized = capacityState.Trim().ToUpperInvariant();
            query = query.Where(designer => designer.CapacityState == normalized);
        }

        return query;
    }

    private static IQueryable<AvailableDesignerReadModel> ApplyWorkloadSort(
        IQueryable<AvailableDesignerReadModel> query,
        string sortBy)
    {
        if (string.Equals(sortBy, SortAvailableSlotDesc, StringComparison.OrdinalIgnoreCase))
        {
            return query
                .OrderByDescending(designer => designer.AvailableSlot)
                .ThenBy(designer => designer.FullName)
                .ThenBy(designer => designer.Email);
        }

        return query
            .OrderByDescending(designer => designer.DesignActiveCount)
            .ThenBy(designer => designer.FullName)
            .ThenBy(designer => designer.Email);
    }

    private IQueryable<DesignerAssignedProjectReadModel> BuildDesignerAssignedProjectsQuery(
        Guid designerId,
        string? bucket)
    {
        var projects =
            from project in DbContext.ProjectSet
            where project.AssignedDesignerId == designerId
            join customer in DbContext.AccountSet on project.CustomerId equals customer.AccountId
            join sales in DbContext.AccountSet on project.AssignedSalesId equals sales.AccountId into salesJoin
            from sales in salesJoin.DefaultIfEmpty()
            select new DesignerAssignedProjectReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Status = project.Status,
                DesignerAssignedAt = project.DesignerAssignedAt,
                CustomerId = project.CustomerId,
                CustomerName = customer.FullName,
                AssignedSalesId = project.AssignedSalesId,
                SalesName = sales != null ? sales.FullName : null
            };

        if (string.IsNullOrWhiteSpace(bucket))
        {
            return projects;
        }

        var normalized = bucket.Trim().ToUpperInvariant();
        return normalized switch
        {
            DesignerWorkloadStatusSets.BucketDesignActive => projects.Where(project =>
                project.Status.HasValue &&
                DesignerWorkloadStatusSets.DesignActive.Contains(project.Status.Value)),
            DesignerWorkloadStatusSets.BucketPostDesign => projects.Where(project =>
                project.Status.HasValue &&
                DesignerWorkloadStatusSets.PostDesign.Contains(project.Status.Value)),
            DesignerWorkloadStatusSets.BucketTerminal => projects.Where(project =>
                project.Status.HasValue &&
                DesignerWorkloadStatusSets.Terminal.Contains(project.Status.Value)),
            DesignerWorkloadStatusSets.BucketOther => projects.Where(project =>
                !project.Status.HasValue ||
                (
                    !DesignerWorkloadStatusSets.DesignActive.Contains(project.Status.Value) &&
                    !DesignerWorkloadStatusSets.PostDesign.Contains(project.Status.Value) &&
                    !DesignerWorkloadStatusSets.Terminal.Contains(project.Status.Value)
                )),
            _ => projects.Where(_ => false)
        };
    }

    private IQueryable<SalesWorkloadItemReadModel> BuildSalesWorkloadQuery(
        int maxActiveProjects,
        string? search,
        string? capacityState,
        string? futurePressureState)
    {
        var query = ProjectActiveSalesWorkload(maxActiveProjects);
        return ApplySalesWorkloadFilters(query, search, capacityState, futurePressureState);
    }

    private IQueryable<SalesWorkloadItemReadModel> ProjectActiveSalesWorkload(int maxActiveProjects)
    {
        var intakeStatuses = SalesWorkloadPressurePolicy.IntakeActive;
        var commercialStatuses = SalesWorkloadPressurePolicy.CommercialActive;
        var designMonitorStatuses = SalesWorkloadPressurePolicy.DesignMonitor;
        var fulfillmentStatuses = SalesWorkloadPressurePolicy.Fulfillment;
        var lifecycleStatuses = SalesWorkloadPressurePolicy.LifecycleAssigned;

        return
            from account in Query()
            join role in DbContext.RoleSet on account.RoleId equals role.RoleId
            where role.RoleName == SalesRoleName &&
                account.Status == AccountStatus.ACTIVE &&
                account.DeletedAt == null
            let intakeCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status.HasValue &&
                intakeStatuses.Contains(project.Status.Value))
            let commercialCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status.HasValue &&
                commercialStatuses.Contains(project.Status.Value))
            let designMonitorCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status.HasValue &&
                designMonitorStatuses.Contains(project.Status.Value))
            let fulfillmentCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status.HasValue &&
                fulfillmentStatuses.Contains(project.Status.Value))
            let lifecycleAssignedCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status.HasValue &&
                lifecycleStatuses.Contains(project.Status.Value))
            let measurementRequiredCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.MEASUREMENT_REQUIRED)
            let spaceVerifiedCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.SPACE_VERIFIED)
            let proposalConsultingCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.PROPOSAL_CONSULTING)
            let inProductionCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.IN_PRODUCTION)
            let productionBlockedCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.PRODUCTION_BLOCKED)
            let readyForDeliveryCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.READY_FOR_DELIVERY)
            let deliveringCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.DELIVERING)
            let deliveredCount = DbContext.ProjectSet.Count(project =>
                project.AssignedSalesId == account.AccountId &&
                project.Status == ProjectStatus.DELIVERED)
            let salesActiveCount = intakeCount + commercialCount
            let futurePressureScore =
                measurementRequiredCount * SalesWorkloadPressurePolicy.WeightMeasurementRequired +
                spaceVerifiedCount * SalesWorkloadPressurePolicy.WeightSpaceVerified +
                proposalConsultingCount * SalesWorkloadPressurePolicy.WeightProposalConsulting +
                inProductionCount * SalesWorkloadPressurePolicy.WeightInProduction +
                productionBlockedCount * SalesWorkloadPressurePolicy.WeightProductionBlocked +
                readyForDeliveryCount * SalesWorkloadPressurePolicy.WeightReadyForDelivery +
                deliveringCount * SalesWorkloadPressurePolicy.WeightDelivering +
                deliveredCount * SalesWorkloadPressurePolicy.WeightDelivered
            select new SalesWorkloadItemReadModel
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Phone = account.Phone,
                AvatarUrl = account.AvatarUrl,
                Status = account.Status,
                IntakeCount = intakeCount,
                CommercialCount = commercialCount,
                DesignMonitorCount = designMonitorCount,
                FulfillmentCount = fulfillmentCount,
                SalesActiveCount = salesActiveCount,
                LifecycleAssignedCount = lifecycleAssignedCount,
                MaxActiveProjects = maxActiveProjects,
                AvailableSlot = maxActiveProjects - salesActiveCount,
                CapacityState = salesActiveCount < maxActiveProjects
                    ? SalesWorkloadPressurePolicy.CapacityAvailableNow
                    : salesActiveCount == maxActiveProjects
                        ? SalesWorkloadPressurePolicy.CapacityFullNow
                        : SalesWorkloadPressurePolicy.CapacityOverNow,
                MeasurementRequiredCount = measurementRequiredCount,
                SpaceVerifiedCount = spaceVerifiedCount,
                ProposalConsultingCount = proposalConsultingCount,
                InProductionCount = inProductionCount,
                ProductionBlockedCount = productionBlockedCount,
                ReadyForDeliveryCount = readyForDeliveryCount,
                DeliveringCount = deliveringCount,
                DeliveredCount = deliveredCount,
                FuturePressureScore = futurePressureScore,
                FuturePressureState = futurePressureScore < SalesWorkloadPressurePolicy.PressureLowMaxExclusive
                    ? SalesWorkloadPressurePolicy.PressureLow
                    : futurePressureScore < SalesWorkloadPressurePolicy.PressureMediumMaxExclusive
                        ? SalesWorkloadPressurePolicy.PressureMedium
                        : SalesWorkloadPressurePolicy.PressureHigh,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };
    }

    private static IQueryable<SalesWorkloadItemReadModel> ApplySalesWorkloadFilters(
        IQueryable<SalesWorkloadItemReadModel> query,
        string? search,
        string? capacityState,
        string? futurePressureState)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = BuildSearchPattern(search);
            query = query.Where(sales =>
                EF.Functions.ILike(sales.Email, pattern) ||
                EF.Functions.ILike(sales.FullName, pattern) ||
                (sales.Phone != null && EF.Functions.ILike(sales.Phone, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(capacityState))
        {
            var normalized = capacityState.Trim().ToUpperInvariant();
            query = query.Where(sales => sales.CapacityState == normalized);
        }

        if (!string.IsNullOrWhiteSpace(futurePressureState))
        {
            var normalized = futurePressureState.Trim().ToUpperInvariant();
            query = query.Where(sales => sales.FuturePressureState == normalized);
        }

        return query;
    }

    private static IQueryable<SalesWorkloadItemReadModel> ApplySalesWorkloadSort(
        IQueryable<SalesWorkloadItemReadModel> query,
        string sortBy)
    {
        if (string.Equals(sortBy, SortSalesActiveCountDesc, StringComparison.OrdinalIgnoreCase))
        {
            return query
                .OrderByDescending(sales => sales.SalesActiveCount)
                .ThenByDescending(sales => sales.FuturePressureScore)
                .ThenBy(sales => sales.FullName);
        }

        if (string.Equals(sortBy, SortAvailableSlotAsc, StringComparison.OrdinalIgnoreCase))
        {
            return query
                .OrderBy(sales => sales.AvailableSlot)
                .ThenByDescending(sales => sales.FuturePressureScore)
                .ThenBy(sales => sales.FullName);
        }

        return query
            .OrderByDescending(sales => sales.FuturePressureScore)
            .ThenByDescending(sales => sales.SalesActiveCount)
            .ThenBy(sales => sales.FullName);
    }

    private IQueryable<SalesAssignedProjectReadModel> BuildSalesAssignedProjectsQuery(
        Guid salesId,
        string? bucket)
    {
        var projects =
            from project in DbContext.ProjectSet
            where project.AssignedSalesId == salesId
            join customer in DbContext.AccountSet on project.CustomerId equals customer.AccountId
            join designer in DbContext.AccountSet on project.AssignedDesignerId equals designer.AccountId into designerJoin
            from designer in designerJoin.DefaultIfEmpty()
            select new SalesAssignedProjectReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                Status = project.Status,
                SalesAssignedAt = project.SalesAssignedAt,
                CustomerId = project.CustomerId,
                CustomerName = customer.FullName,
                AssignedDesignerId = project.AssignedDesignerId,
                DesignerName = designer != null ? designer.FullName : null
            };

        if (string.IsNullOrWhiteSpace(bucket))
        {
            return projects;
        }

        var normalized = bucket.Trim().ToUpperInvariant();
        return normalized switch
        {
            SalesWorkloadPressurePolicy.BucketCurrentActive => projects.Where(project =>
                project.Status.HasValue &&
                (
                    SalesWorkloadPressurePolicy.IntakeActive.Contains(project.Status.Value) ||
                    SalesWorkloadPressurePolicy.CommercialActive.Contains(project.Status.Value)
                )),
            SalesWorkloadPressurePolicy.BucketIntake => projects.Where(project =>
                project.Status.HasValue &&
                SalesWorkloadPressurePolicy.IntakeActive.Contains(project.Status.Value)),
            SalesWorkloadPressurePolicy.BucketCommercial => projects.Where(project =>
                project.Status.HasValue &&
                SalesWorkloadPressurePolicy.CommercialActive.Contains(project.Status.Value)),
            SalesWorkloadPressurePolicy.BucketDesignMonitor => projects.Where(project =>
                project.Status.HasValue &&
                SalesWorkloadPressurePolicy.DesignMonitor.Contains(project.Status.Value)),
            SalesWorkloadPressurePolicy.BucketFulfillment => projects.Where(project =>
                project.Status.HasValue &&
                SalesWorkloadPressurePolicy.Fulfillment.Contains(project.Status.Value)),
            SalesWorkloadPressurePolicy.BucketTerminal => projects.Where(project =>
                project.Status.HasValue &&
                SalesWorkloadPressurePolicy.Terminal.Contains(project.Status.Value)),
            "HIGH_PRESSURE_SOURCE" => projects.Where(project =>
                project.Status == ProjectStatus.PROPOSAL_CONSULTING ||
                project.Status == ProjectStatus.PRODUCTION_BLOCKED),
            SalesWorkloadPressurePolicy.BucketOther => projects.Where(project =>
                !project.Status.HasValue ||
                (
                    !SalesWorkloadPressurePolicy.IntakeActive.Contains(project.Status.Value) &&
                    !SalesWorkloadPressurePolicy.CommercialActive.Contains(project.Status.Value) &&
                    !SalesWorkloadPressurePolicy.DesignMonitor.Contains(project.Status.Value) &&
                    !SalesWorkloadPressurePolicy.Fulfillment.Contains(project.Status.Value) &&
                    !SalesWorkloadPressurePolicy.Terminal.Contains(project.Status.Value)
                )),
            _ => projects.Where(_ => false)
        };
    }

    private IQueryable<UnassignedIntakeProjectReadModel> BuildUnassignedIntakeQuery()
    {
        return
            from project in DbContext.ProjectSet
            where project.AssignedSalesId == null &&
                project.Status == ProjectStatus.SUBMITTED
            join customer in DbContext.AccountSet on project.CustomerId equals customer.AccountId
            select new UnassignedIntakeProjectReadModel
            {
                ProjectId = project.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectName = project.ProjectName,
                BusinessType = project.BusinessType,
                SubmittedAt = project.SubmittedAt,
                CustomerId = project.CustomerId,
                CustomerName = customer.FullName
            };
    }

    private static string BuildSearchPattern(string search)
    {
        return $"%{search.Trim()}%";
    }
}
