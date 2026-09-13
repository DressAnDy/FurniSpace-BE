using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.Common.Search.Documents;
using FurniSpace.Infrastructure.ReadModels.Projects;

namespace FurniSpace.Infrastructure.Common.Search;

public static class ProjectSearchDocumentMapper
{
    public static ProjectSearchDocument ToDocument(ProjectSearchIndexItemReadModel item)
    {
        return new ProjectSearchDocument
        {
            ProjectId = item.ProjectId,
            ProjectCode = item.ProjectCode,
            ProjectName = item.ProjectName,
            BusinessType = item.BusinessType,
            Status = item.Status?.ToString(),
            CustomerId = item.CustomerId,
            CustomerName = item.CustomerName,
            CustomerEmail = item.CustomerEmail,
            CustomerPhone = item.CustomerPhone,
            AssignedSalesId = item.AssignedSalesId,
            AssignedDesignerId = item.AssignedDesignerId,
            SubmittedAt = item.SubmittedAt
        };
    }

    public static ProjectListItemReadModel ToListItem(ProjectSearchDocument document)
    {
        _ = Enum.TryParse<ProjectStatus>(document.Status, ignoreCase: true, out var status);

        return new ProjectListItemReadModel
        {
            ProjectId = document.ProjectId,
            ProjectCode = document.ProjectCode,
            ProjectName = document.ProjectName,
            BusinessType = document.BusinessType,
            Status = document.Status is null ? null : status,
            CustomerId = document.CustomerId,
            AssignedSalesId = document.AssignedSalesId,
            AssignedDesignerId = document.AssignedDesignerId,
            SubmittedAt = document.SubmittedAt
        };
    }
}
