using System.Text.Json;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using InfrastructureRoomPlannerSceneDocument = FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneDocument;
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

    private static InfrastructureRoomPlannerSceneDocument MapToInfrastructureDocument(
        RoomPlannerSceneDocument document)
    {
        var mappedDocument = JsonSerializer.Deserialize<InfrastructureRoomPlannerSceneDocument>(
            JsonSerializer.Serialize(document, JsonOptions),
            JsonOptions) ?? new InfrastructureRoomPlannerSceneDocument();

        NormalizeDynamicJsonValues(mappedDocument);
        return mappedDocument;
    }

    private static void NormalizeDynamicJsonValues(InfrastructureRoomPlannerSceneDocument document)
    {
        document.Objects ??= [];
        document.Layers ??= [];
        document.Camera ??= new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerCameraDocument();
        document.Lighting ??= new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerLightingDocument();
        document.Validation ??= new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerValidationDocument();
        document.SceneLinks ??= new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneLinksDocument();
        document.Metadata ??= new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneMetadataDocument();

        foreach (var sceneObject in document.Objects)
        {
            sceneObject.MaterialOverrides = NormalizeDictionary(sceneObject.MaterialOverrides);
        }

        if (document.BlueprintLayout is not null)
        {
            document.BlueprintLayout.Floors ??= [];
            document.BlueprintLayout.Metadata = NormalizeDictionary(document.BlueprintLayout.Metadata);
            NormalizeBlueprintFloorDictionaries(document.BlueprintLayout.Floors);
        }

        document.Lighting.CustomLights = NormalizeDictionaries(document.Lighting.CustomLights);

        if (document.EditorState is not null)
        {
            document.EditorState.SnapSettings = NormalizeDictionary(document.EditorState.SnapSettings);
        }
    }

    private static void NormalizeBlueprintFloorDictionaries(
        IEnumerable<FurniSpace.Infrastructure.Data.Mongo.RoomPlannerBlueprintFloorDocument> floors)
    {
        foreach (var floor in floors)
        {
            floor.Points ??= [];
            floor.Walls ??= [];
            floor.Doors ??= [];
            floor.Windows ??= [];
            floor.Openings ??= [];
            floor.Rooms = NormalizeDictionaries(floor.Rooms);
            floor.Slabs = NormalizeDictionaries(floor.Slabs);
            floor.Stairs = NormalizeDictionaries(floor.Stairs);
            floor.Balconies = NormalizeDictionaries(floor.Balconies);
            floor.Yards = NormalizeDictionaries(floor.Yards);
            floor.Columns = NormalizeDictionaries(floor.Columns);
            floor.Beams = NormalizeDictionaries(floor.Beams);
        }
    }

    private static List<Dictionary<string, object?>> NormalizeDictionaries(
        IEnumerable<Dictionary<string, object?>>? values) =>
        values is null
            ? []
            : values.Select(NormalizeDictionary).ToList();

    private static Dictionary<string, object?> NormalizeDictionary(Dictionary<string, object?>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values.ToDictionary(
            pair => pair.Key,
            pair => NormalizeDynamicValue(pair.Value));
    }

    private static object? NormalizeDynamicValue(object? value)
    {
        return value is JsonElement element ? NormalizeJsonElement(element) : value;
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => NormalizeJsonElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(NormalizeJsonElement)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integerValue)
                ? integerValue
                : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}
