#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Infrastructure.Data.Mongo;
using FurniSpace.Infrastructure.Repositories.Repository;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Mongo;

public sealed class RoomPlannerSceneRepositoryTests
{
    [Fact]
    public async Task SaveAsync_WhenDocumentHasNoMongoId_UpsertsBySqlSceneId()
    {
        var collection = new FakeRoomPlannerSceneCollection();
        var repository = new RoomPlannerSceneRepository(collection);
        var scene = CreateScene();

        var saved = await repository.UpsertBySqlSceneIdAsync(scene);

        Assert.False(string.IsNullOrWhiteSpace(saved.Id));
        Assert.True(ObjectId.TryParse(saved.Id, out _));
        Assert.Same(scene, collection.UpsertedBySqlSceneId);
        Assert.Null(collection.UpsertedByMongoId);
        Assert.NotNull(saved.Metadata.UpdatedAt);
    }

    [Fact]
    public async Task SaveAsync_WhenSqlSceneAlreadyExists_KeepsExistingMongoId()
    {
        var existingMongoId = ObjectId.GenerateNewId().ToString();
        var scene = CreateScene();
        var existing = CreateScene();
        existing.Id = existingMongoId;
        existing.SqlSceneId = scene.SqlSceneId;
        var collection = new FakeRoomPlannerSceneCollection { SqlScene = existing };
        var repository = new RoomPlannerSceneRepository(collection);

        var saved = await repository.UpsertBySqlSceneIdAsync(scene);

        Assert.Equal(existingMongoId, saved.Id);
        Assert.Same(scene, collection.UpsertedBySqlSceneId);
    }

    [Fact]
    public async Task SaveAsync_WhenDocumentHasMongoId_UpsertsByMongoSceneId()
    {
        var collection = new FakeRoomPlannerSceneCollection();
        var repository = new RoomPlannerSceneRepository(collection);
        var scene = CreateScene();
        scene.Id = ObjectId.GenerateNewId().ToString();

        var saved = await repository.UpsertBySqlSceneIdAsync(scene);

        Assert.Same(scene, saved);
        Assert.Same(scene, collection.UpsertedByMongoId);
        Assert.Null(collection.UpsertedBySqlSceneId);
    }

    [Fact]
    public async Task SaveAsync_WhenMongoIdIsInvalid_ThrowsOperationFailed()
    {
        var repository = new RoomPlannerSceneRepository(new FakeRoomPlannerSceneCollection());
        var scene = CreateScene();
        scene.Id = "not-an-object-id";

        var exception = await Assert.ThrowsAsync<MongoRoomPlannerException>(() => repository.UpsertBySqlSceneIdAsync(scene));

        Assert.Equal(RoomPlannerMongoErrorCodes.OperationFailed, exception.ErrorCode);
    }

    [Fact]
    public async Task GetByMongoSceneIdAsync_WhenIdIsInvalid_ReturnsNullWithoutCollectionCall()
    {
        var collection = new FakeRoomPlannerSceneCollection();
        var repository = new RoomPlannerSceneRepository(collection);

        var scene = await repository.GetByIdAsync("invalid");

        Assert.Null(scene);
        Assert.Equal(0, collection.FindByMongoIdCallCount);
    }

    [Fact]
    public async Task GetByMongoSceneIdAsync_WhenIdIsValid_ReturnsDocument()
    {
        var mongoId = ObjectId.GenerateNewId().ToString();
        var expected = CreateScene();
        expected.Id = mongoId;
        var collection = new FakeRoomPlannerSceneCollection { MongoScene = expected };
        var repository = new RoomPlannerSceneRepository(collection);

        var scene = await repository.GetByIdAsync(mongoId);

        Assert.Same(expected, scene);
        Assert.Equal(mongoId, collection.LastMongoSceneId);
    }

    [Fact]
    public async Task GetBySqlSceneIdAsync_ReturnsDocument()
    {
        var expected = CreateScene();
        var collection = new FakeRoomPlannerSceneCollection { SqlScene = expected };
        var repository = new RoomPlannerSceneRepository(collection);

        var scene = await repository.GetBySqlSceneIdAsync(expected.SqlSceneId);

        Assert.Same(expected, scene);
        Assert.Equal(expected.SqlSceneId, collection.LastSqlSceneId);
    }

    [Fact]
    public async Task EnsureIndexesAsync_DelegatesToCollection()
    {
        var collection = new FakeRoomPlannerSceneCollection();
        var repository = new RoomPlannerSceneRepository(collection);

        await repository.EnsureIndexesAsync();

        Assert.True(collection.EnsureIndexesCalled);
    }

    [Fact]
    public async Task SaveAsync_WhenMongoOperationFails_ReturnsClearOperationError()
    {
        var collection = new FakeRoomPlannerSceneCollection
        {
            OnUpsertBySqlSceneId = () => throw new MongoException("driver failure")
        };
        var repository = new RoomPlannerSceneRepository(collection);

        var exception = await Assert.ThrowsAsync<MongoRoomPlannerException>(() => repository.UpsertBySqlSceneIdAsync(CreateScene()));

        Assert.Equal(RoomPlannerMongoErrorCodes.OperationFailed, exception.ErrorCode);
        Assert.IsType<MongoException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveAsync_WhenCollectionReturnsRoomPlannerError_DoesNotWrapIt()
    {
        var expected = new MongoRoomPlannerException(
            RoomPlannerMongoErrorCodes.ConnectionFailed,
            "connection failed");
        var collection = new FakeRoomPlannerSceneCollection
        {
            OnUpsertBySqlSceneId = () => throw expected
        };
        var repository = new RoomPlannerSceneRepository(collection);

        var exception = await Assert.ThrowsAsync<MongoRoomPlannerException>(() => repository.UpsertBySqlSceneIdAsync(CreateScene()));

        Assert.Same(expected, exception);
    }

    [Fact]
    public async Task SaveAsync_WhenSceneIsNull_ThrowsArgumentNullException()
    {
        var repository = new RoomPlannerSceneRepository(new FakeRoomPlannerSceneCollection());

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpsertBySqlSceneIdAsync(null!));
    }

    [Fact]
    public async Task DeleteBySqlSceneIdAsync_DelegatesToCollection()
    {
        var collection = new FakeRoomPlannerSceneCollection { DeleteResult = true };
        var repository = new RoomPlannerSceneRepository(collection);
        var sqlSceneId = Guid.NewGuid();

        var deleted = await repository.DeleteBySqlSceneIdAsync(sqlSceneId);

        Assert.True(deleted);
        Assert.Equal(sqlSceneId, collection.DeletedSqlSceneId);
    }

    private static RoomPlannerSceneDocument CreateScene() =>
        new()
        {
            SqlSceneId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProjectAreaId = Guid.NewGuid(),
            SchemaVersion = 2,
            Layout = new RoomPlannerLayoutDocument
            {
                Boundary = [new RoomPlannerPoint2Document { X = 0, Z = 0 }]
            }
        };

    private sealed class FakeRoomPlannerSceneCollection : IRoomPlannerSceneCollection
    {
        public bool EnsureIndexesCalled { get; private set; }
        public int FindByMongoIdCallCount { get; private set; }
        public string? LastMongoSceneId { get; private set; }
        public Guid? LastSqlSceneId { get; private set; }
        public RoomPlannerSceneDocument? MongoScene { get; set; }
        public RoomPlannerSceneDocument? SqlScene { get; set; }
        public RoomPlannerSceneDocument? UpsertedByMongoId { get; private set; }
        public RoomPlannerSceneDocument? UpsertedBySqlSceneId { get; private set; }
        public Action? OnUpsertBySqlSceneId { get; set; }
        public bool DeleteResult { get; set; }
        public Guid? DeletedSqlSceneId { get; private set; }

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            EnsureIndexesCalled = true;
            return Task.CompletedTask;
        }

        public Task<RoomPlannerSceneDocument?> FindByMongoIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default)
        {
            FindByMongoIdCallCount++;
            LastMongoSceneId = mongoSceneId;
            return Task.FromResult(MongoScene);
        }

        public Task<RoomPlannerSceneDocument?> FindBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default)
        {
            LastSqlSceneId = sqlSceneId;
            return Task.FromResult(SqlScene);
        }

        public Task<RoomPlannerSceneDocument> UpsertByMongoIdAsync(
            RoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            UpsertedByMongoId = document;
            return Task.FromResult(document);
        }

        public Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
            RoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            OnUpsertBySqlSceneId?.Invoke();
            UpsertedBySqlSceneId = document;
            return Task.FromResult(document);
        }

        public Task<bool> DeleteBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default)
        {
            DeletedSqlSceneId = sqlSceneId;
            return Task.FromResult(DeleteResult);
        }
    }
}
