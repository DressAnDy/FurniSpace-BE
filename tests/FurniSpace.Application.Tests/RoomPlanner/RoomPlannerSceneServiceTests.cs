#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.DTOs.RoomPlanner;
using RoomPlannerSqlSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerProposalSceneRepository;
using Xunit;

namespace FurniSpace.Application.Tests.RoomPlanner;

public sealed class RoomPlannerSceneServiceTests
{
    private static readonly Guid SceneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProposalId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SalesId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid DesignerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProductVersionId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task SaveSceneAsync_AssignedDesignerDraftProposal_UpsertsAndUpdatesSqlMongoSceneId()
    {
        var sql = new FakeSqlSceneRepository { Context = CreateContext(mongoSceneId: null) };
        var documents = new FakeSceneDocumentRepository { SavedId = "64fb8f0f2a98f67b1c000001" };
        var saveCalls = 0;
        var service = CreateService(sql, documents, TestUnitOfWork.ForSaveChanges(_ =>
        {
            saveCalls++;
            return Task.FromResult(1);
        }));

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(SceneId, result.Data!.SceneId);
        Assert.Equal("64fb8f0f2a98f67b1c000001", result.Data.MongoSceneId);
        Assert.NotEqual(default, result.Data.LastSavedAt);
        Assert.NotNull(documents.UpsertedDocument);
        Assert.Equal(SceneId, documents.UpsertedDocument!.SqlSceneId);
        Assert.Equal(ProductVersionId, documents.UpsertedDocument.Objects[0].ProductVersionId);
        Assert.Equal("meter", documents.UpsertedDocument.Unit);
        Assert.Equal("64fb8f0f2a98f67b1c000001", sql.UpdatedMongoSceneId);
        Assert.Equal(1, saveCalls);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenMongoSceneIdAlreadyExists_DoesNotUpdateSql()
    {
        var sql = new FakeSqlSceneRepository { Context = CreateContext("existing-mongo-id") };
        var documents = new FakeSceneDocumentRepository { SavedId = "existing-mongo-id" };
        var service = CreateService(sql, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), SalesId, "SALES");

        Assert.Equal(200, result.Status);
        Assert.Equal("existing-mongo-id", documents.UpsertedDocument!.Id);
        Assert.Null(sql.UpdatedMongoSceneId);
    }

    [Fact]
    public async Task SaveSceneAsync_Customer_ReturnsForbidden()
    {
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(new FakeSqlSceneRepository { Context = CreateContext() }, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), CustomerId, "CUSTOMER");

        Assert.Equal(403, result.Status);
        Assert.Null(documents.UpsertedDocument);
    }

    [Fact]
    public async Task SaveSceneAsync_NonDraftProposal_ReturnsInvalidProposalStatus()
    {
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_PROPOSAL_STATUS", result.ErrorCode);
        Assert.Equal("Room Planner scene can only be saved for draft proposal.", result.Message);
        Assert.Null(documents.UpsertedDocument);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenSceneMissing_ReturnsNotFound()
    {
        var service = CreateService(new FakeSqlSceneRepository(), new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenInputInvalid_ReturnsExpectedErrors()
    {
        var service = CreateService(new FakeSqlSceneRepository(), new FakeSceneDocumentRepository());

        var emptySceneResult = await service.SaveSceneAsync(Guid.Empty, CreateSaveRequest(), DesignerId, "DESIGNER");
        var emptyUserResult = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), Guid.Empty, "DESIGNER");
        var nullRequestResult = await service.SaveSceneAsync(SceneId, null!, DesignerId, "DESIGNER");

        Assert.Equal(400, emptySceneResult.Status);
        Assert.Equal(401, emptyUserResult.Status);
        Assert.Equal(400, nullRequestResult.Status);
    }

    [Fact]
    public async Task GetSceneAsync_CustomerDraftProposal_ReturnsForbidden()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.DRAFT) },
            new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, CustomerId, "CUSTOMER");

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task GetSceneAsync_CustomerPublishedProposal_ReturnsDocument()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000002");
        var service = CreateService(
            new FakeSqlSceneRepository
            {
                Context = CreateContext(document.Id, ProposalStatus.PUBLISHED)
            },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, CustomerId, "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.Equal(document.Id, result.Data!.MongoSceneId);
        Assert.Equal(ProductVersionId, result.Data.Objects[0].ProductVersionId);
    }

    [Fact]
    public async Task GetSceneAsync_WhenNoMongoSceneId_ReturnsEmptyTemplate()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(mongoSceneId: null) },
            new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Null(result.Data!.MongoSceneId);
        Assert.Empty(result.Data.Objects);
    }

    [Fact]
    public async Task GetSceneAsync_WhenDocumentMissing_ReturnsNotFound()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext("missing") },
            new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task GetSceneAsync_WhenInputInvalid_ReturnsExpectedErrors()
    {
        var service = CreateService(new FakeSqlSceneRepository(), new FakeSceneDocumentRepository());

        var emptySceneResult = await service.GetSceneAsync(Guid.Empty, DesignerId, "DESIGNER");
        var emptyUserResult = await service.GetSceneAsync(SceneId, Guid.Empty, "DESIGNER");

        Assert.Equal(400, emptySceneResult.Status);
        Assert.Equal(401, emptyUserResult.Status);
    }

    private static RoomPlannerSceneService CreateService(
        FakeSqlSceneRepository sql,
        FakeSceneDocumentRepository documents,
        FurniSpace.Infrastructure.Persistence.IUnitOfWork? unitOfWork = null) =>
        new(sql, documents, unitOfWork ?? TestUnitOfWork.Instance);

    private static RoomPlannerSceneContextReadModel CreateContext(
        string? mongoSceneId = "64fb8f0f2a98f67b1c000000",
        ProposalStatus status = ProposalStatus.DRAFT) =>
        new()
        {
            SceneId = SceneId,
            ProposalId = ProposalId,
            ProjectId = ProjectId,
            ProjectAreaId = Guid.NewGuid(),
            MongoSceneId = mongoSceneId,
            ProposalStatus = status,
            CustomerId = CustomerId,
            AssignedSalesId = SalesId,
            AssignedDesignerId = DesignerId
        };

    private static SaveRoomPlannerSceneRequestDto CreateSaveRequest() =>
        new()
        {
            SchemaVersion = 2,
            Unit = "meter",
            Layout = new RoomPlannerLayoutDocument
            {
                Type = "WALL_BOUNDARY",
                IsClosed = true,
                AreaSqm = 48.5m,
                Boundary = [new RoomPlannerPoint2Document { X = 0, Z = 0 }],
                Walls =
                [
                    new RoomPlannerWallDocument
                    {
                        WallId = "wall-01",
                        Start = new RoomPlannerPoint2Document { X = 0, Z = 0 },
                        End = new RoomPlannerPoint2Document { X = 8, Z = 0 },
                        Visible = true
                    }
                ],
                Floor = new RoomPlannerFloorDocument { Color = "#C8A676", MaterialCode = "WOOD_LIGHT" }
            },
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "object-01",
                    ProductVersionId = ProductVersionId,
                    ProposalItemId = Guid.NewGuid(),
                    Transform = new RoomPlannerTransformDocument()
                }
            ],
            Camera = new RoomPlannerCameraDocument { Mode = "ORBIT", Zoom = 1 },
            Lighting = new RoomPlannerLightingDocument { Preset = "DEFAULT", AmbientIntensity = 0.8m },
            Validation = new RoomPlannerValidationDocument { Status = "VALID" },
            EditorState = new RoomPlannerEditorStateDocument { ActiveTool = "SELECT", ViewMode = "THREE_D" }
        };

    private static RoomPlannerSceneDocument CreateDocument(string id)
    {
        var request = CreateSaveRequest();
        return new RoomPlannerSceneDocument
        {
            Id = id,
            SchemaVersion = request.SchemaVersion,
            SqlSceneId = SceneId,
            ProposalId = ProposalId,
            ProjectId = ProjectId,
            Unit = request.Unit,
            Layout = request.Layout,
            Objects = request.Objects,
            Camera = request.Camera,
            Lighting = request.Lighting,
            Validation = request.Validation,
            EditorState = request.EditorState,
            Metadata = new RoomPlannerMetadataDocument { UpdatedAt = DateTime.UtcNow }
        };
    }

    private sealed class FakeSqlSceneRepository : RoomPlannerSqlSceneRepository
    {
        public RoomPlannerSceneContextReadModel? Context { get; set; }
        public string? UpdatedMongoSceneId { get; private set; }

        public Task<RoomPlannerSceneContextReadModel?> GetContextAsync(
            Guid sceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Context);

        public Task UpdateMongoSceneIdAsync(
            Guid sceneId,
            string mongoSceneId,
            CancellationToken cancellationToken = default)
        {
            UpdatedMongoSceneId = mongoSceneId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSceneDocumentRepository : IRoomPlannerSceneRepository
    {
        public string SavedId { get; set; } = "64fb8f0f2a98f67b1c000099";
        public RoomPlannerSceneDocument? UpsertedDocument { get; private set; }
        public RoomPlannerSceneDocument? DocumentById { get; set; }

        public Task<RoomPlannerSceneDocument?> GetByIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DocumentById);

        public Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DocumentById);

        public Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
            RoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            document.Id = SavedId;
            UpsertedDocument = document;
            return Task.FromResult(document);
        }

        public Task<bool> DeleteBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
