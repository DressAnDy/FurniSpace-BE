using System.Text.Json;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using InfrastructureRoomPlannerSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerSceneRepository;

namespace FurniSpace.Application.Services.RoomPlanner;

public sealed class RoomPlannerSceneRepositoryAdapter : IRoomPlannerSceneRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly InfrastructureRoomPlannerSceneRepository _inner;

    public RoomPlannerSceneRepositoryAdapter(
        InfrastructureRoomPlannerSceneRepository inner)
    {
        _inner = inner;
    }

    public async Task<RoomPlannerSceneDocument?> GetByIdAsync(
        string mongoSceneId,
        CancellationToken cancellationToken = default)
    {
        var document = await _inner.GetByIdAsync(mongoSceneId, cancellationToken);
        return document is null ? null : MapToApplicationDocument(document);
    }

    public async Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default)
    {
        var document = await _inner.GetBySqlSceneIdAsync(sqlSceneId, cancellationToken);
        return document is null ? null : MapToApplicationDocument(document);
    }

    public async Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default)
    {
        var saved = await _inner.UpsertBySqlSceneIdAsync(
            MapToInfrastructureDocument(document),
            cancellationToken);

        return MapToApplicationDocument(saved);
    }

    public Task<bool> DeleteBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default) =>
        _inner.DeleteBySqlSceneIdAsync(sqlSceneId, cancellationToken);

    private static RoomPlannerSceneDocument MapToApplicationDocument(
        FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneDocument document) =>
        JsonSerializer.Deserialize<RoomPlannerSceneDocument>(
            JsonSerializer.Serialize(document, JsonOptions),
            JsonOptions) ?? new RoomPlannerSceneDocument();

    private static FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneDocument MapToInfrastructureDocument(
        RoomPlannerSceneDocument document) =>
        JsonSerializer.Deserialize<FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneDocument>(
            JsonSerializer.Serialize(document, JsonOptions),
            JsonOptions) ?? new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneDocument();
}
