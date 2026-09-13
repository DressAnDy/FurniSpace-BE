using FurniSpace.Domain.Enums;

namespace FurniSpace.Infrastructure.ReadModels.OperationalDelayReports;

public sealed class OperationalDelayReportDetailReadModel : OperationalDelayReportListItemReadModel
{
    public string? ProjectName { get; init; }
}
