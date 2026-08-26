#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Reports;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class AdminProjectReportRepository : IAdminProjectReportRepository
{
    private readonly AppDbContext _db;

    public AdminProjectReportRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminProjectReportCandidateReadModel>> GetCandidatesAsync(
        AdminProjectReportListQueryReadModel query,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var projectQuery = BuildBaseQuery(query);
        var projectIds = await projectQuery
            .Select(p => p.ProjectId)
            .ToListAsync(cancellationToken);

        if (projectIds.Count == 0)
        {
            return [];
        }

        return await LoadCandidatesAsync(projectIds, utcNow, cancellationToken);
    }

    public async Task<AdminProjectReportCandidateReadModel?> GetCandidateAsync(
        Guid projectId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var rows = await LoadCandidatesAsync([projectId], utcNow, cancellationToken);
        return rows.FirstOrDefault();
    }

    private IQueryable<Domain.Entities.Project> BuildBaseQuery(AdminProjectReportListQueryReadModel query)
    {
        var projects = _db.ProjectSet.AsNoTracking().AsQueryable();

        if (query.ExcludeTerminal)
        {
            projects = projects.Where(p =>
                p.Status != ProjectStatus.COMPLETED &&
                p.Status != ProjectStatus.REJECTED);
        }

        if (query.ProjectStatus.HasValue)
        {
            projects = projects.Where(p => p.Status == query.ProjectStatus);
        }

        if (query.StageStatuses is { Count: > 0 })
        {
            var statuses = query.StageStatuses;
            projects = projects.Where(p => p.Status.HasValue && statuses.Contains(p.Status.Value));
        }

        if (query.SalesId.HasValue)
        {
            projects = projects.Where(p => p.AssignedSalesId == query.SalesId);
        }

        if (query.DesignerId.HasValue)
        {
            projects = projects.Where(p => p.AssignedDesignerId == query.DesignerId);
        }

        if (query.FromUtc.HasValue)
        {
            var from = query.FromUtc.Value;
            projects = projects.Where(p => (p.SubmittedAt ?? p.CreatedAt) >= from);
        }

        if (query.ToUtcExclusive.HasValue)
        {
            var to = query.ToUtcExclusive.Value;
            projects = projects.Where(p => (p.SubmittedAt ?? p.CreatedAt) < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            projects =
                from p in projects
                join customer in _db.AccountSet.AsNoTracking() on p.CustomerId equals customer.AccountId into customers
                from customer in customers.DefaultIfEmpty()
                where (p.ProjectCode != null && p.ProjectCode.ToLower().Contains(keyword))
                      || p.ProjectName.ToLower().Contains(keyword)
                      || (customer != null && customer.FullName.ToLower().Contains(keyword))
                select p;
        }

        return projects;
    }

    private async Task<IReadOnlyList<AdminProjectReportCandidateReadModel>> LoadCandidatesAsync(
        IReadOnlyList<Guid> projectIds,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var projects = await (
            from p in _db.ProjectSet.AsNoTracking()
            where projectIds.Contains(p.ProjectId)
            join customer in _db.AccountSet.AsNoTracking() on p.CustomerId equals customer.AccountId into customers
            from customer in customers.DefaultIfEmpty()
            join sales in _db.AccountSet.AsNoTracking() on p.AssignedSalesId equals sales.AccountId into salesJoin
            from sales in salesJoin.DefaultIfEmpty()
            join designer in _db.AccountSet.AsNoTracking() on p.AssignedDesignerId equals designer.AccountId into designerJoin
            from designer in designerJoin.DefaultIfEmpty()
            select new
            {
                p.ProjectId,
                p.ProjectCode,
                p.ProjectName,
                p.Status,
                p.BusinessType,
                p.ProjectAddress,
                p.RejectionReason,
                p.CustomerId,
                CustomerName = customer != null ? customer.FullName : null,
                p.AssignedSalesId,
                AssignedSalesName = sales != null ? sales.FullName : null,
                p.AssignedDesignerId,
                AssignedDesignerName = designer != null ? designer.FullName : null,
                p.SubmittedAt,
                p.SalesAssignedAt,
                p.ApprovedAt,
                p.DesignerAssignedAt,
                p.CompletedAt,
                p.RejectedAt,
                p.CreatedAt,
                p.UpdatedAt
            }).ToListAsync(cancellationToken);

        var startFees = await _db.PaymentSet.AsNoTracking()
            .Where(pay =>
                projectIds.Contains(pay.ProjectId) &&
                pay.PaymentType == PaymentType.PROJECT_START_FEE)
            .Select(pay => new { pay.ProjectId, pay.Status, pay.CreatedAt, pay.PaidAt })
            .ToListAsync(cancellationToken);

        var startFeeByProject = startFees
            .GroupBy(x => x.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.PaidAt ?? x.CreatedAt).ThenByDescending(x => x.CreatedAt).First());

        var activePayments = await _db.PaymentSet.AsNoTracking()
            .Where(pay =>
                projectIds.Contains(pay.ProjectId) &&
                (pay.Status == PaymentStatus.PENDING || pay.Status == PaymentStatus.PROCESSING) &&
                (pay.ExpiredAt == null || pay.ExpiredAt > utcNow))
            .Select(pay => new { pay.ProjectId, pay.Status, pay.PaymentType, pay.CreatedAt, pay.PaymentId })
            .ToListAsync(cancellationToken);

        var activeByProject = activePayments
            .GroupBy(x => x.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.PaymentId).First());

        var expiredFlags = await _db.PaymentSet.AsNoTracking()
            .Where(pay =>
                projectIds.Contains(pay.ProjectId) &&
                pay.Status == PaymentStatus.EXPIRED)
            .Select(pay => pay.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var expiredSet = expiredFlags.ToHashSet();

        var quotations = await _db.QuotationSet.AsNoTracking()
            .Where(q => projectIds.Contains(q.ProjectId))
            .Select(q => new { q.ProjectId, q.QuotationId, q.Status, q.CreatedAt, q.UpdatedAt })
            .ToListAsync(cancellationToken);

        var latestQuotationByProject = quotations
            .GroupBy(x => x.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.QuotationId).First());

        var revisionCountByProject = quotations
            .Where(x => x.Status is QuotationStatus.REVISION_REQUESTED or QuotationStatus.REVISED)
            .GroupBy(x => x.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var orders = await _db.OrderSet.AsNoTracking()
            .Where(o => projectIds.Contains(o.ProjectId))
            .Select(o => new
            {
                o.ProjectId,
                o.OrderId,
                o.Status,
                o.RemainingAmount,
                o.ConfirmedAt,
                o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var latestOrderByProject = orders
            .GroupBy(x => x.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.ConfirmedAt ?? x.CreatedAt)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.OrderId)
                    .First());

        var productionRequests = await _db.ProductionRequestSet.AsNoTracking()
            .Where(r => projectIds.Contains(r.ProjectId))
            .Select(r => new { r.ProjectId, r.ProductionRequestId, r.CreatedAt })
            .ToListAsync(cancellationToken);

        var latestProductionByProject = productionRequests
            .GroupBy(x => x.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.ProductionRequestId).First());

        var cancelledCounts = await (
            from item in _db.ProductionItemSet.AsNoTracking()
            join req in _db.ProductionRequestSet.AsNoTracking() on item.ProductionRequestId equals req.ProductionRequestId
            where projectIds.Contains(req.ProjectId) && item.Status == ProductionItemStatus.CANCELLED
            group item by req.ProjectId
            into g
            select new { ProjectId = g.Key, Count = g.Count() }
        ).ToListAsync(cancellationToken);
        var cancelledByProject = cancelledCounts.ToDictionary(x => x.ProjectId, x => x.Count);

        var overdueMeasurement = await _db.ProjectScheduleSet.AsNoTracking()
            .Where(s =>
                projectIds.Contains(s.ProjectId) &&
                s.ScheduleType == ProjectScheduleType.MEASUREMENT &&
                s.Status != ProjectScheduleStatus.COMPLETED &&
                s.Status != ProjectScheduleStatus.CANCELLED &&
                s.ScheduledEnd != null &&
                s.ScheduledEnd < utcNow)
            .Select(s => s.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var overdueMeasurementSet = overdueMeasurement.ToHashSet();

        var overdueDelivery = await _db.ProjectScheduleSet.AsNoTracking()
            .Where(s =>
                projectIds.Contains(s.ProjectId) &&
                (s.ScheduleType == ProjectScheduleType.DELIVERY || s.ScheduleType == ProjectScheduleType.HANDOVER) &&
                s.Status != ProjectScheduleStatus.COMPLETED &&
                s.Status != ProjectScheduleStatus.CANCELLED &&
                s.ScheduledEnd != null &&
                s.ScheduledEnd < utcNow)
            .Select(s => s.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var overdueDeliverySet = overdueDelivery.ToHashSet();

        return projects.Select(p =>
        {
            startFeeByProject.TryGetValue(p.ProjectId, out var startFee);
            activeByProject.TryGetValue(p.ProjectId, out var active);
            latestQuotationByProject.TryGetValue(p.ProjectId, out var quotation);
            latestOrderByProject.TryGetValue(p.ProjectId, out var order);
            latestProductionByProject.TryGetValue(p.ProjectId, out var production);
            revisionCountByProject.TryGetValue(p.ProjectId, out var revisionCount);
            cancelledByProject.TryGetValue(p.ProjectId, out var cancelledCount);

            return new AdminProjectReportCandidateReadModel
            {
                ProjectId = p.ProjectId,
                ProjectCode = p.ProjectCode,
                ProjectName = p.ProjectName,
                Status = p.Status,
                BusinessType = p.BusinessType,
                ProjectAddress = p.ProjectAddress,
                RejectionReason = p.RejectionReason,
                CustomerId = p.CustomerId,
                CustomerName = p.CustomerName,
                AssignedSalesId = p.AssignedSalesId,
                AssignedSalesName = p.AssignedSalesName,
                AssignedDesignerId = p.AssignedDesignerId,
                AssignedDesignerName = p.AssignedDesignerName,
                SubmittedAt = p.SubmittedAt,
                SalesAssignedAt = p.SalesAssignedAt,
                ApprovedAt = p.ApprovedAt,
                DesignerAssignedAt = p.DesignerAssignedAt,
                CompletedAt = p.CompletedAt,
                RejectedAt = p.RejectedAt,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                ProjectStartFeeStatus = startFee?.Status,
                ActivePaymentCreatedAt = active?.CreatedAt,
                ActivePaymentStatus = active?.Status,
                ActivePaymentType = active?.PaymentType,
                HasExpiredCollectiblePayment = expiredSet.Contains(p.ProjectId),
                QuotationRevisionRequestedCount = revisionCount,
                LatestQuotationId = quotation?.QuotationId,
                LatestOrderId = order?.OrderId,
                LatestOrderStatus = order?.Status,
                LatestOrderRemainingAmount = order?.RemainingAmount,
                LatestProductionRequestId = production?.ProductionRequestId,
                CancelledProductionItemCount = cancelledCount,
                HasOverdueMeasurementSchedule = overdueMeasurementSet.Contains(p.ProjectId),
                HasOverdueDeliverySchedule = overdueDeliverySet.Contains(p.ProjectId)
            };
        }).ToList();
    }
}
