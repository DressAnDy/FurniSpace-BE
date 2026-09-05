using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Data;
using FurniSpace.Infrastructure.ReadModels.ProductIssues;
using FurniSpace.Infrastructure.Repositories.Base;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FurniSpace.Infrastructure.Repositories.Repository;

public sealed class DeliveryProductIssueReportRepository
    : GenericRepository<DeliveryProductIssueReport>, IDeliveryProductIssueReportRepository
{
    private const string IssueReportReferenceType = "DELIVERY_PRODUCT_ISSUE_REPORT";

    public DeliveryProductIssueReportRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<DeliveryProductIssueReportDetailReadModel?> GetDetailAsync(
        Guid issueId,
        CancellationToken cancellationToken = default)
    {
        var detail = await DbContext.DeliveryProductIssueReportSet
            .Where(issue => issue.DeliveryProductIssueReportId == issueId)
            .Join(
                DbContext.ProjectSet,
                issue => issue.ProjectId,
                project => project.ProjectId,
                (issue, project) => new { issue, project })
            .Join(
                DbContext.OrderItemSet,
                item => item.issue.OrderItemId,
                orderItem => orderItem.OrderItemId,
                (item, orderItem) => new { item.issue, item.project, orderItem })
            .Select(item => new DeliveryProductIssueReportDetailReadModel
            {
                DeliveryProductIssueReportId = item.issue.DeliveryProductIssueReportId,
                ProjectId = item.issue.ProjectId,
                ProjectName = item.project.ProjectName,
                OrderId = item.issue.OrderId,
                OrderItemId = item.issue.OrderItemId,
                DeliveryItemId = item.issue.DeliveryItemId,
                IssueType = item.issue.IssueType,
                Description = item.issue.Description,
                AffectedQuantity = item.issue.AffectedQuantity,
                ReportedBy = item.issue.ReportedBy,
                ReporterName = DbContext.AccountSet
                    .Where(account => account.AccountId == item.issue.ReportedBy)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                ReportedAt = item.issue.ReportedAt,
                CreatedAt = item.issue.CreatedAt,
                ProductNameSnapshot = item.orderItem.ProductNameSnapshot
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var evidenceFiles = await DbContext.FileLinkSet
            .Where(link =>
                link.ReferenceType == IssueReportReferenceType &&
                link.ReferenceId == issueId &&
                link.FileType == FileType.PRODUCT_ISSUE_EVIDENCE)
            .Join(
                DbContext.StoredFileSet,
                link => link.FileId,
                file => file.FileId,
                (link, file) => new DeliveryProductIssueEvidenceReadModel
                {
                    FileId = file.FileId,
                    FileLinkId = link.FileLinkId,
                    OriginalFileName = file.OriginalFileName,
                    FileUrl = file.FileUrl,
                    MimeType = file.MimeType,
                    FileSizeBytes = file.FileSizeBytes
                })
            .OrderBy(file => file.OriginalFileName)
            .ToListAsync(cancellationToken);

        detail.EvidenceFiles = evidenceFiles;
        return detail;
    }

    public Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetByOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return GetListAsync(query => query.Where(issue => issue.OrderId == orderId), cancellationToken);
    }

    public Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return GetListAsync(query => query.Where(issue => issue.ProjectId == projectId), cancellationToken);
    }

    private async Task<IReadOnlyList<DeliveryProductIssueReportListItemReadModel>> GetListAsync(
        Func<IQueryable<DeliveryProductIssueReport>, IQueryable<DeliveryProductIssueReport>> filter,
        CancellationToken cancellationToken)
    {
        var query = filter(DbContext.DeliveryProductIssueReportSet);
        return await query
            .OrderByDescending(issue => issue.ReportedAt)
            .ThenByDescending(issue => issue.DeliveryProductIssueReportId)
            .Select(issue => new DeliveryProductIssueReportListItemReadModel
            {
                DeliveryProductIssueReportId = issue.DeliveryProductIssueReportId,
                ProjectId = issue.ProjectId,
                OrderId = issue.OrderId,
                OrderItemId = issue.OrderItemId,
                DeliveryItemId = issue.DeliveryItemId,
                IssueType = issue.IssueType,
                Description = issue.Description,
                AffectedQuantity = issue.AffectedQuantity,
                ReportedBy = issue.ReportedBy,
                ReporterName = DbContext.AccountSet
                    .Where(account => account.AccountId == issue.ReportedBy)
                    .Select(account => account.FullName)
                    .FirstOrDefault(),
                ReportedAt = issue.ReportedAt,
                CreatedAt = issue.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
