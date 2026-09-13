using FurniSpace.Domain.Enums;
using FurniSpace.Shared.DTOs.Projects;

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectByUserItemDto
    : ProjectByUserItemBaseDto<ProjectStatus?, ProjectCustomerSummaryDto, ProjectAccountSummaryDto>
{
}
