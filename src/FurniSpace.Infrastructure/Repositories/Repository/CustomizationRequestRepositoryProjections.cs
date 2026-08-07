using System.Linq.Expressions;
using System.Reflection;
using FurniSpace.Infrastructure.ReadModels.CustomizationRequests;

namespace FurniSpace.Infrastructure.Repositories.Repository;

internal static class CustomizationRequestRepositoryProjections
{
    private static readonly PropertyInfo[] ReadModelCopyProperties =
        typeof(CustomizationRequestReadModel).GetProperties(BindingFlags.Instance | BindingFlags.Public);

    internal sealed class RequestProjectJoin
    {
        public required Domain.Entities.CustomizationRequest Request { get; init; }

        public required Domain.Entities.Project Project { get; init; }
    }

    internal static readonly Expression<Func<RequestProjectJoin, CustomizationRequestReadModel>> RequestProjectReadModel =
        joined => new CustomizationRequestReadModel
        {
            CustomizationRequestId = joined.Request.CustomizationRequestId,
            ProjectId = joined.Request.ProjectId,
            ProposalId = joined.Request.ProposalId,
            SourceProductVersionId = joined.Request.SourceProductVersionId,
            RequestedByCustomerId = joined.Request.RequestedByCustomerId,
            RequestTitle = joined.Request.RequestTitle,
            RequestDescription = joined.Request.RequestDescription,
            RequestedWidth = joined.Request.RequestedWidth,
            RequestedHeight = joined.Request.RequestedHeight,
            RequestedDepth = joined.Request.RequestedDepth,
            RequestedMaterial = joined.Request.RequestedMaterial,
            RequestedColor = joined.Request.RequestedColor,
            RequestedChangeNote = joined.Request.RequestedChangeNote,
            AcceptedRequestVersionId = joined.Request.AcceptedRequestVersionId,
            Status = joined.Request.Status,
            CreatedAt = joined.Request.CreatedAt,
            UpdatedAt = joined.Request.UpdatedAt,
            CustomerId = joined.Project.CustomerId,
            ProjectName = joined.Project.ProjectName,
            AssignedSalesId = joined.Project.AssignedSalesId,
            AssignedDesignerId = joined.Project.AssignedDesignerId
        };

    internal static CustomizationRequestDetailReadModel ToDetailReadModel(
        CustomizationRequestReadModel source)
    {
        var detail = new CustomizationRequestDetailReadModel();
        CopyReadModelValues(source, detail);
        return detail;
    }

    private static void CopyReadModelValues(
        CustomizationRequestReadModel source,
        CustomizationRequestReadModel destination)
    {
        foreach (var property in ReadModelCopyProperties)
        {
            if (!property.CanRead || !property.CanWrite)
            {
                continue;
            }

            property.SetValue(destination, property.GetValue(source));
        }
    }
}
