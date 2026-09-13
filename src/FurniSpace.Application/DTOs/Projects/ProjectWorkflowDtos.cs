#nullable enable

namespace FurniSpace.Application.DTOs.Projects;

public sealed class ProjectWorkflowDto
{
    public Guid ProjectId { get; init; }
    public string? ProjectCode { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string? CurrentStatus { get; init; }
    public string? CurrentStage { get; init; }
    public bool IsRejected { get; init; }
    public ProjectWorkflowOwnersDto Owners { get; init; } = new();
    public IReadOnlyList<ProjectWorkflowStageDto> Stages { get; init; } = [];
}

public sealed class ProjectWorkflowOwnersDto
{
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? AssignedSalesId { get; init; }
    public string? SalesName { get; init; }
    public Guid? AssignedDesignerId { get; init; }
    public string? DesignerName { get; init; }
}

public sealed class ProjectWorkflowStageDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? StatusInStage { get; init; }
    public ProjectWorkflowStageSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ProjectWorkflowMetricDto> Metrics { get; init; } = [];
    public IReadOnlyList<ProjectWorkflowLinkDto> Links { get; init; } = [];
    public IReadOnlyDictionary<string, object?> Facts { get; init; } =
        new Dictionary<string, object?>();
}

public sealed class ProjectWorkflowStageSummaryDto
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int BlockerCount { get; init; }
    public string? PrimaryOwnerName { get; init; }
}

public sealed class ProjectWorkflowMetricDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public object? Value { get; init; }
    public string? Unit { get; init; }
}

public sealed class ProjectWorkflowLinkDto
{
    public string Type { get; init; } = string.Empty;
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
}
