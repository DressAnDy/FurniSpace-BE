using FurniSpace.Domain.Enums;
using FurniSpace.Shared.DTOs.Projects;

namespace FurniSpace.Infrastructure.ReadModels.Projects;

public sealed class ProjectByUserItemReadModel
    : ProjectByUserItemBaseDto<ProjectStatus?, ProjectCustomerSummaryReadModel, ProjectAccountSummaryReadModel>
{
}
