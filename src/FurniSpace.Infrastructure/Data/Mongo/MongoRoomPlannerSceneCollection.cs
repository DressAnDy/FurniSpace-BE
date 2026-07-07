using FurniSpace.Infrastructure.Common.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FurniSpace.Infrastructure.Data.Mongo;

public sealed class MongoRoomPlannerSceneCollection : IRoomPlannerSceneCollection
{
    private readonly IMongoDatabaseProvider _databaseProvider;
    private readonly MongoDbSettings _settings;
    private IMongoCollection<RoomPlannerSceneDocument>? _collection;

    public MongoRoomPlannerSceneCollection(
        IMongoDatabaseProvider databaseProvider,
        IOptions<MongoDbSettings> settings)
    {
        _databaseProvider = databaseProvider;
        _settings = settings.Value;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        var indexes = new[]
        {
            CreateIndex(
                Builders<RoomPlannerSceneDocument>.IndexKeys.Ascending(scene => scene.SqlSceneId),
                "ux_room_planner_scenes_sql_scene_id",
                isUnique: true),
            CreateIndex(
                Builders<RoomPlannerSceneDocument>.IndexKeys.Ascending(scene => scene.ProposalId),
                "ix_room_planner_scenes_proposal_id"),
            CreateIndex(
                Builders<RoomPlannerSceneDocument>.IndexKeys.Ascending(scene => scene.ProjectId),
                "ix_room_planner_scenes_project_id"),
            CreateIndex(
                Builders<RoomPlannerSceneDocument>.IndexKeys.Ascending(scene => scene.ProjectAreaId),
                "ix_room_planner_scenes_project_area_id"),
            CreateIndex(
                Builders<RoomPlannerSceneDocument>.IndexKeys.Ascending("metadata.updatedAt"),
                "ix_room_planner_scenes_metadata_updated_at")
        };

        await collection.Indexes.CreateManyAsync(indexes, cancellationToken).ConfigureAwait(false);
    }

    public Task<RoomPlannerSceneDocument?> FindByMongoIdAsync(
        string mongoSceneId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomPlannerSceneDocument>.Filter.Eq(scene => scene.Id, mongoSceneId);
        return FirstOrDefaultAsync(filter, cancellationToken);
    }

    public Task<RoomPlannerSceneDocument?> FindBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomPlannerSceneDocument>.Filter.Eq(scene => scene.SqlSceneId, sqlSceneId);
        return FirstOrDefaultAsync(filter, cancellationToken);
    }

    public async Task<RoomPlannerSceneDocument> UpsertByMongoIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomPlannerSceneDocument>.Filter.Eq(scene => scene.Id, document.Id);
        await ReplaceAsync(filter, document, cancellationToken).ConfigureAwait(false);
        return document;
    }

    public async Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomPlannerSceneDocument>.Filter.Eq(scene => scene.SqlSceneId, document.SqlSceneId);
        await ReplaceAsync(filter, document, cancellationToken).ConfigureAwait(false);
        return document;
    }

    public async Task<bool> DeleteBySqlSceneIdAsync(
        Guid sqlSceneId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomPlannerSceneDocument>.Filter.Eq(scene => scene.SqlSceneId, sqlSceneId);
        var result = await GetCollection().DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    private async Task<RoomPlannerSceneDocument?> FirstOrDefaultAsync(
        FilterDefinition<RoomPlannerSceneDocument> filter,
        CancellationToken cancellationToken)
    {
        using var cursor = await GetCollection()
            .FindAsync(filter, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<ReplaceOneResult> ReplaceAsync(
        FilterDefinition<RoomPlannerSceneDocument> filter,
        RoomPlannerSceneDocument document,
        CancellationToken cancellationToken) =>
        GetCollection().ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    private IMongoCollection<RoomPlannerSceneDocument> GetCollection()
    {
        if (_collection is not null)
        {
            return _collection;
        }

        var collectionName = string.IsNullOrWhiteSpace(_settings.RoomPlannerScenesCollectionName)
            ? "room_planner_scenes"
            : _settings.RoomPlannerScenesCollectionName;

        _collection = _databaseProvider.GetDatabase().GetCollection<RoomPlannerSceneDocument>(collectionName);
        return _collection;
    }

    private static CreateIndexModel<RoomPlannerSceneDocument> CreateIndex(
        IndexKeysDefinition<RoomPlannerSceneDocument> keys,
        string name,
        bool isUnique = false) =>
        new(keys, new CreateIndexOptions { Name = name, Unique = isUnique });
}
