namespace FurniSpace.Infrastructure.ReadModels.ProjectChatMessages;

public sealed class ProjectChatMessageQueryReadModel
{
    public int Page { get; init; }
    public int Limit { get; init; }
    public bool SortDescending { get; init; }
}
