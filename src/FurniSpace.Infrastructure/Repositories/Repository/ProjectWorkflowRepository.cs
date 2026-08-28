#nullable enable

using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.Projects;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class ProjectWorkflowRepository : IProjectWorkflowRepository
{
    private readonly AppDbContext _db;

    public ProjectWorkflowRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProjectWorkflowSnapshotReadModel?> GetSnapshotAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.ProjectSet
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new
            {
                p.ProjectId,
                p.ProjectCode,
                p.ProjectName,
                p.Status,
                p.BusinessType,
                p.SubmittedAt,
                p.SalesAssignedAt,
                p.DesignerAssignedAt,
                p.RejectedAt,
                p.CustomerId,
                p.AssignedSalesId,
                p.AssignedDesignerId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        var accountIds = new List<Guid> { project.CustomerId };
        if (project.AssignedSalesId.HasValue)
        {
            accountIds.Add(project.AssignedSalesId.Value);
        }

        if (project.AssignedDesignerId.HasValue)
        {
            accountIds.Add(project.AssignedDesignerId.Value);
        }

        var proposals = await _db.ProposalSet
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new ProjectWorkflowProposalReadModel
            {
                ProposalId = p.ProposalId,
                ProposalName = p.ProposalName,
                Status = p.Status,
                VersionNo = p.VersionNo,
                UpdatedAt = p.UpdatedAt,
                SelectedAt = p.SelectedAt
            })
            .ToListAsync(cancellationToken);

        var quotations = await _db.QuotationSet
            .AsNoTracking()
            .Where(q => q.ProjectId == projectId)
            .Select(q => new ProjectWorkflowQuotationReadModel
            {
                QuotationId = q.QuotationId,
                QuotationCode = q.QuotationCode,
                Status = q.Status,
                TotalAmount = q.TotalAmount,
                SentAt = q.SentAt,
                UpdatedAt = q.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var orders = await _db.OrderSet
            .AsNoTracking()
            .Where(o => o.ProjectId == projectId)
            .Select(o => new ProjectWorkflowOrderReadModel
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                Status = o.Status,
                RemainingAmount = o.RemainingAmount,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var orderIds = orders.ConvertAll(o => o.OrderId);
        var orderItems = orderIds.Count == 0
            ? []
            : await _db.OrderItemSet
                .AsNoTracking()
                .Where(i => orderIds.Contains(i.OrderId))
                .Select(i => new ProjectWorkflowOrderItemReadModel
                {
                    OrderId = i.OrderId,
                    Quantity = i.Quantity,
                    Status = i.Status,
                    DeliveredAt = i.DeliveredAt
                })
                .ToListAsync(cancellationToken);

        var productionRequests = await _db.ProductionRequestSet
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Select(r => new ProjectWorkflowProductionRequestReadModel
            {
                ProductionRequestId = r.ProductionRequestId,
                ProductionCode = r.ProductionCode,
                Status = r.Status,
                AssignedTo = r.AssignedTo,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var productionDeadline = await _db.ProjectPhaseTimelineSet
            .AsNoTracking()
            .Where(timeline =>
                timeline.ProjectId == projectId &&
                timeline.Phase == ProjectPhaseType.PRODUCTION)
            .Select(timeline => (DateOnly?)timeline.DueDate)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var assignedTo in productionRequests
                     .Where(r => r.AssignedTo.HasValue)
                     .Select(r => r.AssignedTo!.Value))
        {
            accountIds.Add(assignedTo);
        }

        var productionRequestIds = productionRequests.ConvertAll(r => r.ProductionRequestId);
        var productionItems = productionRequestIds.Count == 0
            ? []
            : await _db.ProductionItemSet
                .AsNoTracking()
                .Where(i => productionRequestIds.Contains(i.ProductionRequestId))
                .Select(i => new ProjectWorkflowProductionItemReadModel
                {
                    ProductionRequestId = i.ProductionRequestId,
                    Status = i.Status
                })
                .ToListAsync(cancellationToken);

        var schedules = await _db.ProjectScheduleSet
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .Select(s => new ProjectWorkflowScheduleReadModel
            {
                ScheduleId = s.ScheduleId,
                Title = s.Title,
                ScheduleType = s.ScheduleType,
                Status = s.Status,
                ScheduledStart = s.ScheduledStart,
                ScheduledEnd = s.ScheduledEnd
            })
            .ToListAsync(cancellationToken);

        var payments = await _db.PaymentSet
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new ProjectWorkflowPaymentReadModel
            {
                PaymentId = p.PaymentId,
                PaymentCode = p.PaymentCode,
                PaymentType = p.PaymentType,
                Status = p.Status,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var names = await _db.AccountSet
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.AccountId))
            .Select(a => new { a.AccountId, a.FullName })
            .ToDictionaryAsync(a => a.AccountId, a => a.FullName, cancellationToken);

        string? NameOf(Guid? id) =>
            id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

        var productionWithNames = productionRequests
            .Select(r => new ProjectWorkflowProductionRequestReadModel
            {
                ProductionRequestId = r.ProductionRequestId,
                ProductionCode = r.ProductionCode,
                Status = r.Status,
                ProductionDeadline = productionDeadline,
                AssignedTo = r.AssignedTo,
                AssignedToName = NameOf(r.AssignedTo),
                CreatedAt = r.CreatedAt
            })
            .ToList();

        return new ProjectWorkflowSnapshotReadModel
        {
            ProjectId = project.ProjectId,
            ProjectCode = project.ProjectCode,
            ProjectName = project.ProjectName,
            Status = project.Status,
            BusinessType = project.BusinessType,
            SubmittedAt = project.SubmittedAt,
            SalesAssignedAt = project.SalesAssignedAt,
            DesignerAssignedAt = project.DesignerAssignedAt,
            RejectedAt = project.RejectedAt,
            CustomerId = project.CustomerId,
            CustomerName = NameOf(project.CustomerId),
            AssignedSalesId = project.AssignedSalesId,
            SalesName = NameOf(project.AssignedSalesId),
            AssignedDesignerId = project.AssignedDesignerId,
            DesignerName = NameOf(project.AssignedDesignerId),
            Proposals = proposals,
            Quotations = quotations,
            Orders = orders,
            OrderItems = orderItems,
            ProductionRequests = productionWithNames,
            ProductionItems = productionItems,
            Schedules = schedules,
            Payments = payments
        };
    }
}
