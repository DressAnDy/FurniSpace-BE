using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class OperationalDelayReportRepository
    : GenericRepository<OperationalDelayReport>, IOperationalDelayReportRepository
{
    public OperationalDelayReportRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public Task<OperationalDelayReportDetailReadModel?> GetDetailAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        return DbContext.OperationalDelayReportSet
            .Where(report => report.OperationalDelayReportId == reportId)
            .Join(
                DbContext.ProjectSet,
                report => report.ProjectId,
                project => project.ProjectId,
                (report, project) => new { report, project })
            .Select(item => new OperationalDelayReportDetailReadModel
            {
                OperationalDelayReportId = item.report.OperationalDelayReportId,
                ProjectId = item.report.ProjectId,
                ProjectName = item.project.ProjectName,
                ReportPhase = item.report.ReportPhase,
                ProductionRequestId = item.report.ProductionRequestId,
                OrderId = item.report.OrderId,
                DeliveryId = item.report.DeliveryId,
                DeadlineSnapshot = item.report.DeadlineSnapshot,
                DelayState = item.report.DelayState,
                ProductionReasonCode = item.report.ProductionReasonCode,
                DeliveryReasonCode = item.report.DeliveryReasonCode,
                ReasonDetail = item.report.ReasonDetail,
                ReportedBy = item.report.ReportedBy,
                ReporterName = DbContext.AccountSet
                    .Where(account => account.AccountId == item.report.ReportedBy)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                ReportedAt = item.report.ReportedAt,
                CreatedAt = item.report.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalDelayReportListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        OperationalDelayPhase phase,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.OperationalDelayReportSet
            .Where(report => report.ProjectId == projectId && report.ReportPhase == phase)
            .OrderByDescending(report => report.ReportedAt)
            .ThenByDescending(report => report.OperationalDelayReportId)
            .Select(report => new OperationalDelayReportListItemReadModel
            {
                OperationalDelayReportId = report.OperationalDelayReportId,
                ProjectId = report.ProjectId,
                ReportPhase = report.ReportPhase,
                ProductionRequestId = report.ProductionRequestId,
                OrderId = report.OrderId,
                DeliveryId = report.DeliveryId,
                DeadlineSnapshot = report.DeadlineSnapshot,
                DelayState = report.DelayState,
                ProductionReasonCode = report.ProductionReasonCode,
                DeliveryReasonCode = report.DeliveryReasonCode,
                ReasonDetail = report.ReasonDetail,
                ReportedBy = report.ReportedBy,
                ReporterName = DbContext.AccountSet
                    .Where(account => account.AccountId == report.ReportedBy)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                ReportedAt = report.ReportedAt,
                CreatedAt = report.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
