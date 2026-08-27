using FurniSpace.Application.Common.Projects;
using FurniSpace.Application.Interfaces.Payments;
using FurniSpace.Application.Interfaces.Projects;
using FurniSpace.Infrastructure.Persistence;

namespace FurniSpace.Application.Common.Payments;

public sealed record PaymentServiceDependencies(
    IUnitOfWork UnitOfWork,
    SePayOptions SePayOptions,
    PayOsOptions PayOsOptions,
    ProjectWorkflowSettings ProjectWorkflowSettings,
    SePayVietQrUrlBuilder VietQrUrlBuilder,
    IPayOsClient PayOsClient,
    IProjectPhaseDeadlineService PhaseDeadlines);
