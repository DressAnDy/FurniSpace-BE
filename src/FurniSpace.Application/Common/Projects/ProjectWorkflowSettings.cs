namespace FurniSpace.Application.Common.Projects;

public sealed class ProjectWorkflowSettings
{
    public const string SectionName = "ProjectWorkflow";

    public bool RequireMeasurementFileOnScheduleComplete { get; set; }

    public bool RequireMeasurementFileOnProposalConsulting { get; set; }

    public decimal DefaultProjectStartFeeAmount { get; set; } = 2_000_000m;
}
