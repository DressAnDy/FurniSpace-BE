using FurniSpace.Domain.Enums;
using FurniSpace.Shared.DTOs.Projects;

namespace FurniSpace.Infrastructure.DTOs.Projects;

public sealed class ProjectByUserItemReadModel
    : ProjectByUserItemBaseDto<ProjectStatus?, ProjectCustomerSummaryReadModel, ProjectAccountSummaryReadModel>
{
}
