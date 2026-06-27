namespace FurniSpace.Application.Common.Projects;

public sealed class ProjectWorkflowSettings
{
    public const string SectionName = "ProjectWorkflow";

    public bool RequireMeasurementFileOnScheduleComplete { get; set; }

    public bool RequireMeasurementFileOnProposalDrafting { get; set; }
}
