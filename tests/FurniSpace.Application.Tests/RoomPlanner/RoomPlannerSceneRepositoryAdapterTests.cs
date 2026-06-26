#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Domain.Enums;
using InfrastructureRoomPlannerSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerSceneRepository;
using InfrastructureRoomPlannerSceneDocument = FurniSpace.Infrastructure.Mongo.RoomPlannerSceneDocument;
using Xunit;

namespace FurniSpace.Application.Tests.RoomPlanner;

public sealed class RoomPlannerSceneRepositoryAdapterTests
{
    [Fact]
    public async Task GetByIdAsync_WhenInnerReturnsDocument_MapsToApplicationDocument()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository
        {
            DocumentById = CreateInfrastructureDocument("mongo-id")
        };
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);

        var result = await adapter.GetByIdAsync("mongo-id");

        Assert.NotNull(result);
        Assert.Equal("mongo-id", result.Id);
        Assert.Equal(inner.DocumentById.SqlSceneId, result.SqlSceneId);
        Assert.Equal("meter", result.Unit);
        Assert.Single(result.Objects);
    }

    [Fact]
    public async Task GetByIdAsync_WhenInnerReturnsNull_ReturnsNull()
    {
        var adapter = new RoomPlannerSceneRepositoryAdapter(new FakeInfrastructureRoomPlannerSceneRepository());

        var result = await adapter.GetByIdAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySqlSceneIdAsync_MapsToApplicationDocument()
    {
        var sqlSceneId = Guid.NewGuid();
        var inner = new FakeInfrastructureRoomPlannerSceneRepository
        {
            DocumentBySqlSceneId = CreateInfrastructureDocument("mongo-id", sqlSceneId)
        };
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);

        var result = await adapter.GetBySqlSceneIdAsync(sqlSceneId);

        Assert.NotNull(result);
        Assert.Equal(sqlSceneId, result.SqlSceneId);
    }

    [Fact]
    public async Task UpsertBySqlSceneIdAsync_MapsToInfrastructureAndBack()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var document = CreateApplicationDocument("app-mongo-id");

        var result = await adapter.UpsertBySqlSceneIdAsync(document);

        Assert.NotNull(inner.UpsertedDocument);
        Assert.Equal(document.SqlSceneId, inner.UpsertedDocument.SqlSceneId);
        Assert.Equal(document.Objects[0].ObjectId, inner.UpsertedDocument.Objects[0].ObjectId);
        Assert.Equal("saved-mongo-id", result.Id);
    }

    [Fact]
    public async Task DeleteBySqlSceneIdAsync_DelegatesToInnerRepository()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository { DeleteResult = true };
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var sqlSceneId = Guid.NewGuid();

        var result = await adapter.DeleteBySqlSceneIdAsync(sqlSceneId);

        Assert.True(result);
        Assert.Equal(sqlSceneId, inner.DeletedSqlSceneId);
    }

    private static RoomPlannerSceneDocument CreateApplicationDocument(string id)
    {
        return new RoomPlannerSceneDocument
        {
            Id = id,
            SchemaVersion = 2,
            SqlSceneId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Unit = "meter",
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "object-01",
                    ProductVersionId = Guid.NewGuid(),
                    Transform = new RoomPlannerTransformDocument()
                }
            ],
            Metadata = new RoomPlannerMetadataDocument { UpdatedAt = DateTime.UtcNow }
        };
    }

    private static InfrastructureRoomPlannerSceneDocument CreateInfrastructureDocument(
        string id,
        Guid? sqlSceneId = null)
    {
        return new InfrastructureRoomPlannerSceneDocument
        {
            Id = id,
            SchemaVersion = 2,
            SqlSceneId = sqlSceneId ?? Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Unit = "meter",
            Objects =
            [
                new FurniSpace.Infrastructure.Mongo.RoomPlannerObjectDocument
                {
                    ObjectId = "object-01",
                    ProductVersionId = Guid.NewGuid(),
                    Transform = new FurniSpace.Infrastructure.Mongo.RoomPlannerTransformDocument()
                }
            ],
            Metadata = new FurniSpace.Infrastructure.Mongo.RoomPlannerSceneMetadataDocument
            {
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    private sealed class FakeInfrastructureRoomPlannerSceneRepository : InfrastructureRoomPlannerSceneRepository
    {
        public InfrastructureRoomPlannerSceneDocument? DocumentById { get; set; }
        public InfrastructureRoomPlannerSceneDocument? DocumentBySqlSceneId { get; set; }
        public InfrastructureRoomPlannerSceneDocument? UpsertedDocument { get; private set; }
        public Guid DeletedSqlSceneId { get; private set; }
        public bool DeleteResult { get; set; }

        public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<InfrastructureRoomPlannerSceneDocument?> GetByIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DocumentById);
        }

        public Task<InfrastructureRoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DocumentBySqlSceneId);
        }

        public Task<InfrastructureRoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
            InfrastructureRoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            UpsertedDocument = document;
            document.Id = "saved-mongo-id";
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
