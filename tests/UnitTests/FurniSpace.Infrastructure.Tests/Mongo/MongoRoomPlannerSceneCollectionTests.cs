#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Common.Mongo;
using FurniSpace.Infrastructure.Data.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Mongo;

public sealed class MongoRoomPlannerSceneCollectionTests
{
    [Fact]
    public async Task EnsureIndexesAsync_CreatesManagedIndexesOnConfiguredCollection()
    {
        var document = CreateDocument();
        var collectionProxy = CreateCollectionProxy(document);
        var databaseProxy = CreateDatabaseProxy(collectionProxy.Collection);
        var collection = new MongoRoomPlannerSceneCollection(
            new FakeMongoDatabaseProvider(databaseProxy.Database),
            Options.Create(new MongoDbSettings { RoomPlannerScenesCollectionName = "custom_scenes" }));

        await collection.EnsureIndexesAsync();

        Assert.Equal("custom_scenes", databaseProxy.Proxy.RequestedCollectionName);
        Assert.Equal(9, collectionProxy.IndexProxy.CreatedIndexes.Count);
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ux_room_planner_scenes_sql_scene_id" && index.Options.Unique == true);
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ix_room_planner_scenes_scene_links_project_area_ids");
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ix_room_planner_scenes_blueprint_floors_project_area_id");
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ix_room_planner_scenes_objects_product_version_id");
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ix_room_planner_scenes_objects_proposal_item_id");
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ix_room_planner_scenes_objects_layout_asset_id");
        Assert.Contains(collectionProxy.IndexProxy.CreatedIndexes, index => index.Options?.Name == "ix_room_planner_scenes_metadata_updated_at");
    }

    [Fact]
    public async Task FindUpsertAndDelete_DelegateToMongoCollection()
    {
        var document = CreateDocument();
        var collectionProxy = CreateCollectionProxy(document, deletedCount: 1);
        var databaseProxy = CreateDatabaseProxy(collectionProxy.Collection);
        var collection = new MongoRoomPlannerSceneCollection(
            new FakeMongoDatabaseProvider(databaseProxy.Database),
            Options.Create(new MongoDbSettings { RoomPlannerScenesCollectionName = "" }));

        var byMongoId = await collection.FindByMongoIdAsync(document.Id!);
        var bySqlId = await collection.FindBySqlSceneIdAsync(document.SqlSceneId);
        var upsertedByMongoId = await collection.UpsertByMongoIdAsync(document);
        var upsertedBySqlId = await collection.UpsertBySqlSceneIdAsync(document);
        var deleted = await collection.DeleteBySqlSceneIdAsync(document.SqlSceneId);

        Assert.Equal("room_planner_scenes", databaseProxy.Proxy.RequestedCollectionName);
        Assert.Same(document, byMongoId);
        Assert.Same(document, bySqlId);
        Assert.Same(document, upsertedByMongoId);
        Assert.Same(document, upsertedBySqlId);
        Assert.True(deleted);
        Assert.Equal(2, collectionProxy.Proxy.ReplaceCallCount);
        Assert.Equal(1, collectionProxy.Proxy.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteBySqlSceneId_WhenNoDocumentDeleted_ReturnsFalse()
    {
        var collectionProxy = CreateCollectionProxy(CreateDocument(), deletedCount: 0);
        var databaseProxy = CreateDatabaseProxy(collectionProxy.Collection);
        var collection = new MongoRoomPlannerSceneCollection(
            new FakeMongoDatabaseProvider(databaseProxy.Database),
            Options.Create(new MongoDbSettings()));

        var deleted = await collection.DeleteBySqlSceneIdAsync(Guid.NewGuid());

        Assert.False(deleted);
    }

    private static RoomPlannerSceneDocument CreateDocument()
        => new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            SqlSceneId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid()
        };

    private static CollectionProxyHandle CreateCollectionProxy(
        RoomPlannerSceneDocument document,
        long deletedCount = 1)
    {
        var collection = DispatchProxy.Create<IMongoCollection<RoomPlannerSceneDocument>, MongoCollectionProxy>();
        var proxy = (MongoCollectionProxy)(object)collection;
        proxy.Document = document;
        proxy.DeletedCount = deletedCount;

        var indexManager = DispatchProxy.Create<IMongoIndexManager<RoomPlannerSceneDocument>, MongoIndexManagerProxy>();
        proxy.IndexManager = indexManager;
        var indexProxy = (MongoIndexManagerProxy)(object)indexManager;

        return new CollectionProxyHandle(collection, proxy, indexProxy);
    }

    private static DatabaseProxyHandle CreateDatabaseProxy(IMongoCollection<RoomPlannerSceneDocument> collection)
    {
        var database = DispatchProxy.Create<IMongoDatabase, MongoDatabaseProxy>();
        var proxy = (MongoDatabaseProxy)(object)database;
        proxy.Collection = collection;
        return new DatabaseProxyHandle(database, proxy);
    }

    private sealed class FakeMongoDatabaseProvider : IMongoDatabaseProvider
    {
        private readonly IMongoDatabase _database;

        public FakeMongoDatabaseProvider(IMongoDatabase database)
        {
            _database = database;
        }

        public IMongoDatabase GetDatabase() => _database;
    }

    private class MongoDatabaseProxy : DispatchProxy
    {
        public IMongoCollection<RoomPlannerSceneDocument>? Collection { get; set; }
        public string? RequestedCollectionName { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IMongoDatabase.GetCollection))
            {
                RequestedCollectionName = (string?)args?[0];
                return Collection;
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private class MongoCollectionProxy : DispatchProxy
    {
        public RoomPlannerSceneDocument? Document { get; set; }
        public long DeletedCount { get; set; }
        public IMongoIndexManager<RoomPlannerSceneDocument>? IndexManager { get; set; }
        public int ReplaceCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Indexes" => IndexManager,
                nameof(IMongoCollection<RoomPlannerSceneDocument>.FindAsync) =>
                    Task.FromResult<IAsyncCursor<RoomPlannerSceneDocument>>(new SingleBatchCursor(Document is null ? [] : [Document])),
                nameof(IMongoCollection<RoomPlannerSceneDocument>.ReplaceOneAsync) => ReplaceOne(),
                nameof(IMongoCollection<RoomPlannerSceneDocument>.DeleteOneAsync) => DeleteOne(),
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private Task<ReplaceOneResult> ReplaceOne()
        {
            ReplaceCallCount++;
            return Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null));
        }

        private Task<DeleteResult> DeleteOne()
        {
            DeleteCallCount++;
            return Task.FromResult<DeleteResult>(new DeleteResult.Acknowledged(DeletedCount));
        }
    }

    private class MongoIndexManagerProxy : DispatchProxy
    {
        public List<CreateIndexModel<RoomPlannerSceneDocument>> CreatedIndexes { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IMongoIndexManager<RoomPlannerSceneDocument>.CreateManyAsync))
            {
                CreatedIndexes.AddRange(((IEnumerable<CreateIndexModel<RoomPlannerSceneDocument>>)args![0]!).ToArray());
                return Task.FromResult<IEnumerable<string>>(CreatedIndexes.Select(index => index.Options?.Name ?? string.Empty).ToArray());
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private sealed class SingleBatchCursor : IAsyncCursor<RoomPlannerSceneDocument>
    {
        private readonly IReadOnlyList<RoomPlannerSceneDocument> _documents;
        private bool _moved;

        public SingleBatchCursor(IReadOnlyList<RoomPlannerSceneDocument> documents)
        {
            _documents = documents;
        }

        public IEnumerable<RoomPlannerSceneDocument> Current => _moved ? _documents : [];
        public bool MoveNext(CancellationToken cancellationToken = default) => MoveNextCore();
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNextCore());
        public void Dispose() { }

        private bool MoveNextCore()
        {
            if (_moved)
            {
                return false;
            }

            _moved = true;
            return _documents.Count > 0;
        }
    }

    private sealed record CollectionProxyHandle(
        IMongoCollection<RoomPlannerSceneDocument> Collection,
        MongoCollectionProxy Proxy,
        MongoIndexManagerProxy IndexProxy);

    private sealed record DatabaseProxyHandle(IMongoDatabase Database, MongoDatabaseProxy Proxy);
}
