#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.Proposals;
using FurniSpace.Infrastructure.ReadModels.RoomPlanner;
using FurniSpace.Infrastructure.Repositories.IRepository;
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
    private static readonly Guid ProjectAreaId = Guid.Parse("88888888-8888-8888-8888-888888888888");

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
        Assert.Equal("ROOM_PLANNER_BABYLON_V1", documents.UpsertedDocument.EditorVersion);
        Assert.Null(documents.UpsertedDocument.Layout);
        Assert.Equal(ProjectAreaId, documents.UpsertedDocument.SceneLinks.ProjectAreaIds[0]);
        Assert.Equal("floor-01", documents.UpsertedDocument.BlueprintLayout!.Floors[0].Id);
        Assert.Equal("p1", documents.UpsertedDocument.BlueprintLayout.Floors[0].Points[0].PointId);
        Assert.Equal("w1", documents.UpsertedDocument.BlueprintLayout.Floors[0].Doors[0].WallId);
        Assert.Equal(1.4m, documents.UpsertedDocument.BlueprintLayout.Floors[0].Doors[0].Offset);
        Assert.Equal("floor-01", documents.UpsertedDocument.Objects[0].FloorId);
        Assert.Equal("FLOOR", documents.UpsertedDocument.Objects[0].Placement.Mode);
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

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

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
    public async Task SaveSceneAsync_AssignedSales_ReturnsForbidden()
    {
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(new FakeSqlSceneRepository { Context = CreateContext() }, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), SalesId, "SALES");

        Assert.Equal(403, result.Status);
        Assert.Null(documents.UpsertedDocument);
    }

    [Fact]
    public async Task SaveSceneAsync_RevisionRequestedProposal_SavesScene()
    {
        var sql = new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.REVISION_REQUESTED) };
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(sql, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
    }

    [Fact]
    public async Task SaveSceneAsync_PublishedProposal_SavesScene()
    {
        var sql = new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) };
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(sql, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
    }

    [Fact]
    public async Task SaveSceneAsync_NonEditableProposal_ReturnsProposalNotEditable()
    {
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.SELECTED) },
            documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("PROPOSAL_NOT_EDITABLE", result.ErrorCode);
        Assert.Equal("Room Planner scene can only be saved for editable proposal.", result.Message);
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
    public async Task SaveSceneAsync_WithInvalidProductVersion_ReturnsProductVersionNotFound()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository(),
            productVersions: new FakeProductVersionRepository());

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("PRODUCT_VERSION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WithInvalidModelFile_ReturnsModelFileNotFound()
    {
        var productVersions = new FakeProductVersionRepository();
        productVersions.ValidProductVersionIds.Add(ProductVersionId);
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository(),
            productVersions: productVersions,
            projectFiles: new FakeProjectFileRepository());

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("MODEL_FILE_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WithUnlinkedModelFile_ReturnsModelFileNotLinked()
    {
        var request = CreateSaveRequest();
        var modelFileId = request.Objects[0].ModelSnapshot!.ModelFileId!.Value;
        var productVersions = new FakeProductVersionRepository();
        productVersions.ValidProductVersionIds.Add(ProductVersionId);
        var projectFiles = new FakeProjectFileRepository();
        projectFiles.FileMetadataByFileId[modelFileId] = CreateModelFileMetadata(modelFileId);
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository(),
            productVersions: productVersions,
            projectFiles: projectFiles);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("MODEL_FILE_NOT_LINKED", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WithValidModelFile_SavesScene()
    {
        var request = CreateSaveRequest();
        var modelFileId = request.Objects[0].ModelSnapshot!.ModelFileId!.Value;
        var productVersions = new FakeProductVersionRepository();
        productVersions.ValidProductVersionIds.Add(ProductVersionId);
        var projectFiles = new FakeProjectFileRepository();
        projectFiles.FileMetadataByFileId[modelFileId] = CreateModelFileMetadata(modelFileId);
        projectFiles.FileLinksByFileId[modelFileId] =
        [
            new FileLink
            {
                FileId = modelFileId,
                ReferenceType = "PRODUCT_VERSION",
                ReferenceId = ProductVersionId,
                FileType = FileType.MODEL_3D
            }
        ];
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository(),
            productVersions: productVersions,
            projectFiles: projectFiles);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
    }

    [Theory]
    [InlineData(2, "ROOM_PLANNER_SCHEMA_VERSION_UNSUPPORTED")]
    [InlineData(4, "ROOM_PLANNER_SCHEMA_VERSION_UNSUPPORTED")]
    public async Task SaveSceneAsync_WithUnsupportedSchemaVersion_ReturnsExpectedError(
        int schemaVersion,
        string expectedErrorCode)
    {
        var request = CreateSaveRequest();
        request.SchemaVersion = schemaVersion;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenBlueprintLayoutMissing_ReturnsBlueprintLayoutRequired()
    {
        var request = CreateSaveRequest();
        request.BlueprintLayout = null;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("BLUEPRINT_LAYOUT_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenSceneIsNotRoomPlanner_ReturnsRoomPlannerSceneRequired()
    {
        var context = CreateContext();
        context.SceneType = ProposalSceneType.THREE_D;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_SCENE_REQUIRED", result.ErrorCode);
    }

    [Theory]
    [InlineData("missing-mapped-floor")]
    [InlineData("unmapped-floor")]
    public async Task SaveSceneAsync_WhenBlueprintMappingInvalid_ReturnsFloorMappingMismatch(string scenario)
    {
        var request = CreateSaveRequest();
        ApplyInvalidBlueprintScenario(request, scenario);
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("BLUEPRINT_FLOOR_MAPPING_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenUnitMismatch_ReturnsRoomPlannerUnitMismatch()
    {
        var request = CreateSaveRequest();
        ApplyInvalidBlueprintScenario(request, "unit-mismatch");
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_UNIT_MISMATCH", result.ErrorCode);
    }

    [Theory]
    [InlineData("duplicate-floor-id", "DUPLICATE_FLOOR_ID")]
    [InlineData("empty-floors", "BLUEPRINT_FLOOR_REQUIRED")]
    [InlineData("invalid-wall-point", "INVALID_WALL_POINT_REFERENCE")]
    [InlineData("invalid-opening-wall", "INVALID_OPENING_WALL_REFERENCE")]
    [InlineData("duplicate-point-id", "INVALID_BLUEPRINT_GEOMETRY")]
    [InlineData("duplicate-wall-id", "INVALID_BLUEPRINT_GEOMETRY")]
    [InlineData("duplicate-object-id", "DUPLICATE_OBJECT_ID")]
    [InlineData("blank-object-id", "INVALID_BLUEPRINT_GEOMETRY")]
    public async Task SaveSceneAsync_WhenBlueprintGeometryInvalid_ReturnsExpectedError(
        string scenario,
        string expectedErrorCode)
    {
        var request = CreateSaveRequest();
        ApplyInvalidBlueprintScenario(request, scenario);
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenLegacyOpeningOffsetProvided_NormalizesToOffset()
    {
        var request = CreateSaveRequest();
        var door = request.BlueprintLayout!.Floors[0].Doors[0];
        door.Offset = null;
        door.OffsetFromWallStart = 1.9m;
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        var savedDoor = documents.UpsertedDocument!.BlueprintLayout!.Floors[0].Doors[0];
        Assert.Equal(1.9m, savedDoor.Offset);
        Assert.Null(savedDoor.OffsetFromWallStart);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenSceneAreaProjectMismatch_ReturnsProjectAreaProjectMismatch()
    {
        var context = CreateContext();
        context.SceneAreas =
        [
            CreateSceneArea(projectId: Guid.NewGuid())
        ];
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("PROJECT_AREA_PROJECT_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenObjectFloorMissing_ReturnsInvalidObjectFloorReference()
    {
        var request = CreateSaveRequest();
        request.Objects[0].FloorId = "missing-floor";
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_OBJECT_FLOOR_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenWallIdsDuplicateAcrossFloors_SavesSuccessfully()
    {
        var secondAreaId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var context = CreateContext();
        context.SceneAreas =
        [
            CreateSceneArea(),
            new ProposalSceneAreaReadModel
            {
                ProposalSceneAreaId = Guid.NewGuid(),
                SceneId = SceneId,
                ProjectAreaId = secondAreaId,
                ProjectId = ProjectId,
                AreaName = "Second floor",
                SortOrder = 1
            }
        ];
        var request = CreateSaveRequest();
        request.BlueprintLayout!.Floors.Add(new RoomPlannerBlueprintFloorDocument
        {
            Id = "floor-02",
            ProjectAreaId = secondAreaId,
            Name = "Second floor",
            LevelIndex = 1,
            Elevation = 3.12m,
            FloorHeight = 3,
            Points =
            [
                new RoomPlannerPoint2Document { PointId = "p1", X = 0, Z = 0 },
                new RoomPlannerPoint2Document { PointId = "p2", X = 4, Z = 0 }
            ],
            Walls =
            [
                new RoomPlannerWallDocument
                {
                    WallId = "w1",
                    StartPointId = "p1",
                    EndPointId = "p2"
                }
            ]
        });
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(new FakeSqlSceneRepository { Context = context }, documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(2, documents.UpsertedDocument!.BlueprintLayout!.Floors.Count);
        Assert.Equal("w1", documents.UpsertedDocument.BlueprintLayout.Floors[0].Walls[0].WallId);
        Assert.Equal("w1", documents.UpsertedDocument.BlueprintLayout.Floors[1].Walls[0].WallId);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenNullNestedCollections_NormalizesAndSaves()
    {
        var request = CreateSaveRequest();
        request.Objects = null!;
        request.Layers = null!;
        request.BlueprintLayout!.Metadata = null!;
        request.BlueprintLayout.Floors[0].Rooms = null!;
        request.BlueprintLayout.Floors[0].Slabs = null!;
        request.BlueprintLayout.Floors[0].Openings = null!;
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
        Assert.Empty(documents.UpsertedDocument!.Objects);
        Assert.Empty(documents.UpsertedDocument.BlueprintLayout!.Floors[0].Rooms);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenWallUsesCoordinatesOnly_SavesSuccessfully()
    {
        var request = CreateSaveRequest();
        request.BlueprintLayout!.Floors[0].Walls[0].StartPointId = null;
        request.BlueprintLayout.Floors[0].Walls[0].EndPointId = null;
        request.BlueprintLayout.Floors[0].Walls[0].Start = new RoomPlannerPoint2Document { X = 0, Z = 0 };
        request.BlueprintLayout.Floors[0].Walls[0].End = new RoomPlannerPoint2Document { X = 5, Z = 0 };
        request.BlueprintLayout.Floors[0].Doors.Clear();
        request.BlueprintLayout.Floors[0].Windows.Clear();
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Null(documents.UpsertedDocument!.BlueprintLayout!.Floors[0].Walls[0].StartPointId);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenExistingDocumentFoundBySqlSceneId_PreservesCreationMetadataAndLinksSql()
    {
        var createdAt = DateTime.UtcNow.AddDays(-2);
        var creatorId = Guid.NewGuid();
        var existing = CreateDocument("64fb8f0f2a98f67b1c000010");
        existing.Metadata.CreatedAt = createdAt;
        existing.Metadata.CreatedBy = creatorId;
        var sql = new FakeSqlSceneRepository { Context = CreateContext(mongoSceneId: null) };
        var documents = new FakeSceneDocumentRepository
        {
            DocumentBySqlSceneId = existing,
            SavedId = existing.Id!
        };
        var service = CreateService(sql, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(existing.Id, result.Data!.MongoSceneId);
        Assert.Equal(existing.Id, sql.UpdatedMongoSceneId);
        Assert.Equal(createdAt, documents.UpsertedDocument!.Metadata.CreatedAt);
        Assert.Equal(creatorId, documents.UpsertedDocument.Metadata.CreatedBy);
        Assert.Equal(DesignerId, documents.UpsertedDocument.Metadata.UpdatedBy);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenMongoSaveFails_ReturnsRoomPlannerSaveFailed()
    {
        var documents = new FakeSceneDocumentRepository { ThrowOnUpsert = true };
        var service = CreateService(new FakeSqlSceneRepository { Context = CreateContext() }, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(500, result.Status);
        Assert.Equal("ROOM_PLANNER_SAVE_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenSqlLinkFails_ReturnsSqlLinkFailed()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(mongoSceneId: null) },
            new FakeSceneDocumentRepository(),
            TestUnitOfWork.ForSaveChanges(_ => throw new InvalidOperationException("link failed")));

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(500, result.Status);
        Assert.Equal("ROOM_PLANNER_SQL_LINK_FAILED", result.ErrorCode);
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
        Assert.Equal("ROOM_PLANNER_BABYLON_V1", result.Data.EditorVersion); // from saved document
        Assert.Equal("floor-01", result.Data.BlueprintLayout!.Floors[0].Id);
        Assert.Equal("p1", result.Data.BlueprintLayout.Floors[0].Points[0].PointId);
        Assert.Equal("w1", result.Data.BlueprintLayout.Floors[0].Windows[0].WallId);
        Assert.Equal(2.5m, result.Data.BlueprintLayout.Floors[0].Windows[0].Offset);
        Assert.Equal(ProductVersionId, result.Data.Objects[0].ProductVersionId);
        Assert.Equal("FLOOR", result.Data.Objects[0].Placement.Mode);
    }

    [Fact]
    public async Task GetSceneAsync_CustomerRejectedProposal_ReturnsDocument()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000003");
        var service = CreateService(
            new FakeSqlSceneRepository
            {
                Context = CreateContext(document.Id, ProposalStatus.REJECTED)
            },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, CustomerId, "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.Equal(document.Id, result.Data!.MongoSceneId);
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
        Assert.Equal(3, result.Data.SchemaVersion);
        Assert.Equal(ProjectAreaId, result.Data.ProjectAreaIds[0]);
        Assert.Single(result.Data.Areas);
        Assert.Equal(ProjectAreaId, result.Data.Areas[0].ProjectAreaId);
        Assert.Equal("Main cafe area", result.Data.Areas[0].AreaName);
        Assert.Equal(ProjectAreaId, result.Data.BlueprintLayout!.Floors[0].ProjectAreaId);
        Assert.StartsWith("floor-", result.Data.BlueprintLayout.Floors[0].Id, StringComparison.Ordinal);
        Assert.Empty(result.Data.Objects);
    }

    [Fact]
    public async Task GetSceneAsync_ForStandardArea_InitializesLockedRectangleFromAreaDimensions()
    {
        var context = CreateContext(mongoSceneId: null);
        context.SceneAreas = [CreateSceneAreaWithLayout(width: 8m, length: 6m, height: 3.2m, isSpecialLayout: false)];
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        var floor = result.Data!.BlueprintLayout!.Floors[0];
        Assert.False(result.Data.Areas[0].IsSpecialLayout);
        Assert.Equal(4, floor.Points.Count);
        Assert.Equal(4, floor.Walls.Count);
        Assert.All(floor.Walls, wall => Assert.True(wall.Locked));
        Assert.Contains(floor.Points, point => point.X == 8m && point.Z == 6m);
        Assert.Equal(3.2m, floor.FloorHeight);
    }

    [Fact]
    public async Task GetSceneAsync_ForSpecialArea_ReturnsPrimaryBlueprintWithoutFixedRectangle()
    {
        var fileId = Guid.NewGuid();
        var context = CreateContext(mongoSceneId: null);
        context.SceneAreas = [CreateSceneAreaWithLayout(width: 8m, length: 6m, height: 3.2m, isSpecialLayout: true)];
        var projectFiles = new FakeProjectFileRepository();
        projectFiles.CatalogFilesByReferenceId[ProjectAreaId] =
        [
            new CatalogFileReadModel
            {
                FileId = fileId,
                FileLinkId = Guid.NewGuid(),
                ReferenceId = ProjectAreaId,
                FileType = FileType.FLOOR_PLAN,
                OriginalFileName = "area-blueprint.pdf",
                FileUrl = "https://storage.test/area-blueprint.pdf",
                MimeType = "application/pdf",
                Visibility = FileVisibility.STAFF_ONLY,
                IsPrimary = true,
                DisplayOrder = 1,
                UploadedAt = DateTime.UtcNow
            }
        ];
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository(),
            projectFiles: projectFiles);

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        var floor = result.Data!.BlueprintLayout!.Floors[0];
        Assert.True(result.Data.Areas[0].IsSpecialLayout);
        Assert.Empty(floor.Points);
        Assert.Empty(floor.Walls);
        var blueprint = Assert.Single(result.Data.AreaBlueprints);
        Assert.Equal(fileId, blueprint.FileId);
        Assert.True(blueprint.IsPrimary);
    }

    [Fact]
    public async Task SaveSceneAsync_ForStandardAreaWithEditedOuterBoundary_ReturnsInvalidGeometry()
    {
        var context = CreateContext();
        context.SceneAreas = [CreateSceneAreaWithLayout(width: 8m, length: 6m, height: 3.2m, isSpecialLayout: false)];
        var request = CreateSaveRequest();
        SetFloorRectangle(request.BlueprintLayout!.Floors[0], minX: 0m, minZ: 0m, width: 9m, length: 6m);
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(new FakeSqlSceneRepository { Context = context }, documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_BLUEPRINT_GEOMETRY", result.ErrorCode);
        Assert.Null(documents.UpsertedDocument);
    }

    [Fact]
    public async Task SaveSceneAsync_ForStandardAreaWithCenteredBoundary_SavesScene()
    {
        var context = CreateContext();
        context.SceneAreas = [CreateSceneAreaWithLayout(width: 8m, length: 6m, height: 3.2m, isSpecialLayout: false)];
        var request = CreateSaveRequest();
        SetFloorRectangle(request.BlueprintLayout!.Floors[0], minX: -4m, minZ: -3m, width: 8m, length: 6m);
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(new FakeSqlSceneRepository { Context = context }, documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
        Assert.Equal(-4m, documents.UpsertedDocument.BlueprintLayout!.Floors[0].Points[0].X);
        Assert.Equal(3m, documents.UpsertedDocument.BlueprintLayout.Floors[0].Points[2].Z);
    }

    [Fact]
    public async Task SaveSceneAsync_ForStandardAreaWithNonRectangularBoundary_ReturnsInvalidGeometry()
    {
        var context = CreateContext();
        context.SceneAreas = [CreateSceneAreaWithLayout(width: 8m, length: 6m, height: 3.2m, isSpecialLayout: false)];
        var request = CreateSaveRequest();
        SetFloorRectangle(request.BlueprintLayout!.Floors[0], minX: -4m, minZ: -3m, width: 8m, length: 6m);
        request.BlueprintLayout.Floors[0].Points[3].Z = 0m;
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(new FakeSqlSceneRepository { Context = context }, documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("INVALID_BLUEPRINT_GEOMETRY", result.ErrorCode);
        Assert.Null(documents.UpsertedDocument);
    }

    [Fact]
    public async Task GetSceneAsync_WhenNoMongoSceneId_ReturnsStableOrderedEmptyFloors()
    {
        var secondAreaId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var context = CreateContext(mongoSceneId: null);
        context.SceneAreas =
        [
            new ProposalSceneAreaReadModel
            {
                ProposalSceneAreaId = Guid.NewGuid(),
                SceneId = SceneId,
                ProjectAreaId = secondAreaId,
                ProjectId = ProjectId,
                AreaName = "Second floor",
                FloorNumber = 2,
                SortOrder = 1
            },
            new ProposalSceneAreaReadModel
            {
                ProposalSceneAreaId = Guid.NewGuid(),
                SceneId = SceneId,
                ProjectAreaId = ProjectAreaId,
                ProjectId = ProjectId,
                AreaName = "First floor",
                FloorNumber = 1,
                SortOrder = 0
            }
        ];
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository());

        var firstResult = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");
        var secondResult = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(200, firstResult.Status);
        Assert.Equal("ROOM_PLANNER_BABYLON_BUILDING_V1", firstResult.Data!.EditorVersion);
        Assert.Equal([ProjectAreaId, secondAreaId], firstResult.Data.ProjectAreaIds);
        Assert.Equal([ProjectAreaId, secondAreaId], firstResult.Data.Areas.Select(area => area.ProjectAreaId).ToList());
        Assert.Equal([ProjectAreaId, secondAreaId], firstResult.Data.BlueprintLayout!.Floors.Select(floor => floor.ProjectAreaId).ToList());
        Assert.Equal(0m, firstResult.Data.BlueprintLayout.Floors[0].Elevation);
        Assert.Equal(3.12m, firstResult.Data.BlueprintLayout.Floors[1].Elevation);
        Assert.Equal(
            firstResult.Data.BlueprintLayout.Floors.Select(floor => floor.Id).ToList(),
            secondResult.Data!.BlueprintLayout!.Floors.Select(floor => floor.Id).ToList());
    }

    [Fact]
    public async Task GetSceneAsync_WhenDocumentMissing_ReturnsNotFound()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext("missing") },
            new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(404, result.Status);
        Assert.Equal("ROOM_PLANNER_DOCUMENT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetSceneAsync_WhenDocumentInvalid_ReturnsRoomPlannerDocumentInvalid()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000004");
        document.SchemaVersion = 2;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id) },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_DOCUMENT_INVALID", result.ErrorCode);
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

    [Fact]
    public async Task SaveSceneAsync_Admin_CanSaveScene()
    {
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), Guid.NewGuid(), "ADMIN");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
    }

    [Fact]
    public async Task GetSceneAsync_AdminAndAssignedSales_CanViewScene()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000020");
        var context = CreateContext(document.Id, ProposalStatus.DRAFT);
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository { DocumentById = document });

        var adminResult = await service.GetSceneAsync(SceneId, Guid.NewGuid(), "ADMIN");
        var salesResult = await service.GetSceneAsync(SceneId, SalesId, "SALES");

        Assert.Equal(200, adminResult.Status);
        Assert.Equal(200, salesResult.Status);
    }

    [Fact]
    public async Task GetSceneAsync_CustomerSelectedProposal_ReturnsDocument()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000021");
        var service = CreateService(
            new FakeSqlSceneRepository
            {
                Context = CreateContext(document.Id, ProposalStatus.SELECTED)
            },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, CustomerId, "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.Equal(document.Id, result.Data!.MongoSceneId);
    }

    [Fact]
    public async Task GetSceneAsync_WhenMongoLoadThrows_ReturnsRoomPlannerLoadFailed()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext("mongo-id") },
            new FakeSceneDocumentRepository { ThrowOnGet = true });

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(500, result.Status);
        Assert.Equal("ROOM_PLANNER_LOAD_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task GetSceneAsync_WhenSceneMissing_ReturnsNotFound()
    {
        var service = CreateService(new FakeSqlSceneRepository(), new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenSavedIdBlank_ReturnsRoomPlannerSaveFailed()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository { SavedId = " " });

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(500, result.Status);
        Assert.Equal("ROOM_PLANNER_SAVE_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenExistingMetadataNull_UsesCurrentUserAsCreator()
    {
        var existing = CreateDocument("64fb8f0f2a98f67b1c000022");
        existing.Metadata = null!;
        var documents = new FakeSceneDocumentRepository
        {
            DocumentById = existing,
            SavedId = existing.Id!
        };
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(existing.Id) },
            documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(DesignerId, documents.UpsertedDocument!.Metadata.CreatedBy);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenPayloadNeedsFullNormalization_SavesSuccessfully()
    {
        var request = CreateSaveRequest();
        request.Unit = "  METER  ";
        request.Camera = null!;
        request.Lighting = null!;
        request.Validation = null!;
        request.EditorState = new RoomPlannerEditorStateDocument { SnapSettings = null! };
        var floor = request.BlueprintLayout!.Floors[0];
        floor.Points =
        [
            new RoomPlannerPoint2Document { PointId = "p1", X = 0, Z = 0 },
            new RoomPlannerPoint2Document { PointId = "p2", X = 1, Z = 0 }
        ];
        floor.Walls =
        [
            new RoomPlannerWallDocument
            {
                WallId = "w1",
                StartPointId = "p1",
                EndPointId = "p2",
                Start = null!,
                End = null!,
                Style = null!
            }
        ];
        floor.Doors = null!;
        floor.Windows = null!;
        floor.Openings = null!;
        floor.Stairs = null!;
        floor.Balconies = null!;
        floor.Yards = null!;
        floor.Columns = null!;
        floor.Beams = null!;
        request.Objects =
        [
            new RoomPlannerObjectDocument
            {
                ObjectId = "object-01",
                FloorId = "floor-01",
                ProductVersionId = ProductVersionId,
                MaterialOverrides = null!,
                Transform = null!,
                DimensionsSnapshot = null!,
                Placement = null!
            }
        ];
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal("METER", documents.UpsertedDocument!.Unit);
        Assert.NotNull(documents.UpsertedDocument.Camera);
        Assert.NotNull(documents.UpsertedDocument.Objects[0].Placement);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenBlueprintFloorsNull_ReturnsFloorRequired()
    {
        var request = CreateSaveRequest();
        request.BlueprintLayout!.Floors = null!;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository());

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("BLUEPRINT_FLOOR_REQUIRED", result.ErrorCode);
    }

    [Theory]
    [InlineData("blank-floor-id", "BLUEPRINT_FLOOR_REQUIRED")]
    [InlineData("duplicate-project-area", "BLUEPRINT_FLOOR_MAPPING_MISMATCH")]
    [InlineData("partial-wall-point", "INVALID_WALL_POINT_REFERENCE")]
    [InlineData("duplicate-opening-id", "INVALID_BLUEPRINT_GEOMETRY")]
    [InlineData("blank-point-id", "INVALID_BLUEPRINT_GEOMETRY")]
    [InlineData("blank-wall-id", "INVALID_BLUEPRINT_GEOMETRY")]
    [InlineData("blank-opening-id", "INVALID_BLUEPRINT_GEOMETRY")]
    [InlineData("invalid-window-wall", "INVALID_OPENING_WALL_REFERENCE")]
    [InlineData("invalid-openings-wall", "INVALID_OPENING_WALL_REFERENCE")]
    [InlineData("blank-object-floor", "ROOM_PLANNER_OBJECT_FLOOR_NOT_FOUND")]
    [InlineData("empty-product-version", "PRODUCT_VERSION_NOT_FOUND")]
    public async Task SaveSceneAsync_WhenAdditionalValidationFails_ReturnsExpectedError(
        string scenario,
        string expectedErrorCode)
    {
        var request = CreateSaveRequest();
        ApplyInvalidBlueprintScenario(request, scenario);
        var productVersions = new FakeProductVersionRepository { ValidProductVersionIds = { ProductVersionId } };
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            new FakeSceneDocumentRepository(),
            productVersions: productVersions);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WithoutProductVersionRepository_SkipsProductValidation()
    {
        var request = CreateSaveRequest();
        request.Objects[0].ModelSnapshot = null;
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
    }

    [Fact]
    public async Task GetSceneAsync_WhenDocumentHasNullMetadata_ReturnsNullLastSavedAt()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000023");
        document.Metadata = null!;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id, ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, CustomerId, "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.Null(result.Data!.LastSavedAt);
    }

    [Fact]
    public async Task GetSceneAsync_WhenDocumentBlueprintMissing_ReturnsDocumentInvalid()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000024");
        document.BlueprintLayout = null;
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id) },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_DOCUMENT_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task GetSceneAsync_WhenDocumentIdsMismatch_ReturnsDocumentInvalid()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000025");
        document.ProjectId = Guid.NewGuid();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id) },
            new FakeSceneDocumentRepository { DocumentById = document });

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_DOCUMENT_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenLegacyWindowAndOpeningOffsetsProvided_NormalizesToOffset()
    {
        var request = CreateSaveRequest();
        request.BlueprintLayout!.Floors[0].Windows[0].Offset = null;
        request.BlueprintLayout.Floors[0].Windows[0].OffsetFromWallStart = 2.2m;
        request.BlueprintLayout.Floors[0].Openings =
        [
            new RoomPlannerOpeningDocument
            {
                OpeningId = "opening-1",
                Type = "OPENING",
                WallId = "w1",
                OffsetFromWallStart = 0.5m
            }
        ];
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext() },
            documents);

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(2.2m, documents.UpsertedDocument!.BlueprintLayout!.Floors[0].Windows[0].Offset);
        Assert.Equal(0.5m, documents.UpsertedDocument.BlueprintLayout.Floors[0].Openings[0].Offset);
    }

    [Fact]
    public async Task GetSceneAsync_WhenNoMongoSceneId_MapsAreaTypeAndStatus()
    {
        var context = CreateContext(mongoSceneId: null);
        context.SceneAreas =
        [
            new ProposalSceneAreaReadModel
            {
                ProposalSceneAreaId = Guid.NewGuid(),
                SceneId = SceneId,
                ProjectAreaId = ProjectAreaId,
                ProjectId = ProjectId,
                AreaName = "Main cafe area",
                AreaType = ProjectAreaType.ROOM,
                FloorNumber = 1,
                SortOrder = 0,
                Status = ProjectAreaStatus.VERIFIED
            }
        ];
        var service = CreateService(
            new FakeSqlSceneRepository { Context = context },
            new FakeSceneDocumentRepository());

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal("ROOM", result.Data!.Areas[0].AreaType);
        Assert.Equal("VERIFIED", result.Data.Areas[0].Status);
        Assert.Equal(1, result.Data.Areas[0].FloorNumber);
        Assert.Equal("ROOM_PLANNER_BABYLON_BUILDING_V1", result.Data.EditorVersion);
        Assert.Equal(0m, result.Data.BlueprintLayout!.Floors[0].Elevation);
        Assert.NotNull(result.Data.EditorState);
    }

    [Fact]
    public async Task ResolveProductsAsync_CustomerPublishedScene_ReturnsEligibleProducts()
    {
        var document = CreateDocument("64fb8f0f2a98f67b1c000002");
        var productVersions = new FakeProductVersionRepository();
        productVersions.ValidProductVersionIds.Add(ProductVersionId);
        var projectFiles = new FakeProjectFileRepository();
        projectFiles.CatalogFilesByReferenceId[ProductVersionId] =
        [
            new CatalogFileReadModel
            {
                ReferenceId = ProductVersionId,
                FileId = Guid.NewGuid(),
                FileLinkId = Guid.NewGuid(),
                FileType = FileType.MODEL_3D,
                FileUrl = "https://cdn.example.com/model.glb",
                MimeType = "model/gltf-binary",
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                OriginalFileName = "model.glb"
            }
        ];
        var service = CreateService(
            new FakeSqlSceneRepository
            {
                Context = CreateContext(document.Id!, ProposalStatus.PUBLISHED)
            },
            new FakeSceneDocumentRepository { DocumentById = document },
            productVersions: productVersions,
            projectFiles: projectFiles);

        var result = await service.ResolveProductsAsync(
            SceneId,
            new ResolveRoomPlannerProductsRequestDto { ProductVersionIds = [ProductVersionId] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1, productVersions.GetValidDetailsCallCount);
        Assert.Equal([ProductVersionId], productVersions.LastRequestedIds);
        Assert.Equal(1, productVersions.LastReturnedCount);
        Assert.NotEmpty(result.Data!.Items);
        Assert.Equal(ProductVersionId, result.Data.Items[0].ProductVersionId);
    }

    [Fact]
    public async Task ResolveProductsAsync_ProductNotInScene_ReturnsBadRequest()
    {
        var documents = new FakeSceneDocumentRepository { DocumentById = CreateDocument("64fb8f0f2a98f67b1c000000") };
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            documents,
            productVersions: new FakeProductVersionRepository(),
            projectFiles: new FakeProjectFileRepository());

        var result = await service.ResolveProductsAsync(
            SceneId,
            new ResolveRoomPlannerProductsRequestDto { ProductVersionIds = [Guid.NewGuid()] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(400, result.Status);
    }

    [Fact]
    public async Task ResolveProductsAsync_CustomerDraftProposal_ReturnsForbidden()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.DRAFT) },
            new FakeSceneDocumentRepository { DocumentById = CreateDocument("64fb8f0f2a98f67b1c000000") },
            productVersions: new FakeProductVersionRepository(),
            projectFiles: new FakeProjectFileRepository());

        var result = await service.ResolveProductsAsync(
            SceneId,
            new ResolveRoomPlannerProductsRequestDto { ProductVersionIds = [ProductVersionId] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task ResolveProductsAsync_EmptyRequest_ReturnsEmptyItems()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = CreateDocument("64fb8f0f2a98f67b1c000000") },
            productVersions: new FakeProductVersionRepository(),
            projectFiles: new FakeProjectFileRepository());

        var result = await service.ResolveProductsAsync(
            SceneId,
            new ResolveRoomPlannerProductsRequestDto(),
            CustomerId,
            "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_CustomerPublishedScene_ReturnsReferencedAssets()
    {
        var layoutAssetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var document = CreateDocument("64fb8f0f2a98f67b1c000003");
        document.Objects[0].ObjectType = "LAYOUT_ASSET";
        document.Objects[0].LayoutAssetId = layoutAssetId;
        document.Objects[0].LayoutAssetType = "STAIR";
        document.Objects[0].ProductVersionId = null;
        document.Objects[0].ModelSnapshot = null;

        var layoutAssets = new FakeLayoutAssetRepository();
        layoutAssets.Assets[layoutAssetId] = new LayoutAsset
        {
            LayoutAssetId = layoutAssetId,
            AssetCode = "STAIR-01",
            AssetName = "Main stair",
            AssetType = LayoutAssetType.STAIR,
            Status = LayoutAssetStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectFiles = new FakeProjectFileRepository();
        projectFiles.CatalogFilesByReferenceId[layoutAssetId] =
        [
            new CatalogFileReadModel
            {
                ReferenceId = layoutAssetId,
                FileId = Guid.NewGuid(),
                FileLinkId = Guid.NewGuid(),
                FileType = FileType.MODEL_3D,
                FileUrl = "https://cdn.example.com/stair.glb",
                MimeType = "model/gltf-binary",
                Visibility = FileVisibility.CUSTOMER_VISIBLE,
                OriginalFileName = "stair.glb",
                Status = FileStatus.ACTIVE,
                IsPrimary = true
            }
        ];

        var service = CreateService(
            new FakeSqlSceneRepository
            {
                Context = CreateContext(document.Id!, ProposalStatus.PUBLISHED)
            },
            new FakeSceneDocumentRepository { DocumentById = document },
            projectFiles: projectFiles,
            layoutAssets: layoutAssets);

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [layoutAssetId] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal(layoutAssetId, result.Data.Items[0].LayoutAssetId);
        Assert.NotNull(result.Data.Items[0].PrimaryModel);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_AssetNotInScene_ReturnsBadRequest()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = CreateDocument("64fb8f0f2a98f67b1c000004") },
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [Guid.NewGuid()] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_LAYOUT_ASSET_NOT_IN_SCENE", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_CustomerDraftProposal_ReturnsForbidden()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.DRAFT) },
            new FakeSceneDocumentRepository { DocumentById = CreateDocument("64fb8f0f2a98f67b1c000005") },
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [Guid.NewGuid()] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(403, result.Status);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_WhenInputInvalid_ReturnsExpectedErrors()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository(),
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var emptySceneResult = await service.ResolveLayoutAssetsAsync(
            Guid.Empty,
            new ResolveRoomPlannerLayoutAssetsRequestDto(),
            CustomerId,
            "CUSTOMER");
        var emptyUserResult = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto(),
            Guid.Empty,
            "CUSTOMER");
        var nullRequestResult = await service.ResolveLayoutAssetsAsync(
            SceneId,
            null!,
            CustomerId,
            "CUSTOMER");

        Assert.Equal(400, emptySceneResult.Status);
        Assert.Equal(401, emptyUserResult.Status);
        Assert.Equal(400, nullRequestResult.Status);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_WhenDependenciesMissing_ReturnsInternalServerError()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [Guid.NewGuid()] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(500, result.Status);
        Assert.Equal("ROOM_PLANNER_LOAD_FAILED", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_WhenSceneMissing_ReturnsNotFound()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = null },
            new FakeSceneDocumentRepository(),
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [Guid.NewGuid()] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(404, result.Status);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_EmptyRequest_ReturnsEmptyItems()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = CreateDocument("64fb8f0f2a98f67b1c000006") },
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto(),
            CustomerId,
            "CUSTOMER");

        Assert.Equal(200, result.Status);
        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_AssetMissingInDatabase_ReturnsLayoutAssetNotFound()
    {
        var layoutAssetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var document = CreateDocument("64fb8f0f2a98f67b1c000007");
        document.Objects[0].ObjectType = "LAYOUT_ASSET";
        document.Objects[0].LayoutAssetId = layoutAssetId;
        document.Objects[0].LayoutAssetType = "STAIR";
        document.Objects[0].ProductVersionId = null;

        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id!, ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = document },
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [layoutAssetId] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(400, result.Status);
        Assert.Equal("LAYOUT_ASSET_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_Designer_ReturnsStaffVisibleFilesAndBlueprintAssets()
    {
        var objectAssetId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var wallAssetId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var floorAssetId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var document = CreateDocument("64fb8f0f2a98f67b1c000008");
        document.Objects[0].ObjectType = "LAYOUT_ASSET";
        document.Objects[0].LayoutAssetId = objectAssetId;
        document.Objects[0].LayoutAssetType = "STAIR";
        document.Objects[0].ProductVersionId = null;
        document.BlueprintLayout!.Floors[0].Walls[0].Style = new RoomPlannerStyleDocument { LayoutAssetId = wallAssetId };
        document.BlueprintLayout.Floors[0].FloorStyle = new RoomPlannerFloorDocument { LayoutAssetId = floorAssetId };

        var layoutAssets = new FakeLayoutAssetRepository();
        foreach (var assetId in new[] { objectAssetId, wallAssetId, floorAssetId })
        {
            layoutAssets.Assets[assetId] = new LayoutAsset
            {
                LayoutAssetId = assetId,
                AssetCode = $"ASSET-{assetId.ToString()[..8]}",
                AssetName = "Planner asset",
                AssetType = LayoutAssetType.STAIR,
                Status = LayoutAssetStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        var projectFiles = new FakeProjectFileRepository();
        projectFiles.CatalogFilesByReferenceId[objectAssetId] =
        [
            CreateLayoutAssetCatalogFile(objectAssetId, FileType.MODEL_3D, FileVisibility.STAFF_ONLY),
            CreateLayoutAssetCatalogFile(objectAssetId, FileType.TEXTURE, FileVisibility.CUSTOMER_VISIBLE, isPrimary: true),
            CreateLayoutAssetCatalogFile(objectAssetId, FileType.PREVIEW, FileVisibility.CUSTOMER_VISIBLE, isPrimary: true)
        ];

        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id!, ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = document },
            projectFiles: projectFiles,
            layoutAssets: layoutAssets);

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [objectAssetId, wallAssetId, floorAssetId] },
            DesignerId,
            "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(3, result.Data!.Items.Count);
        var resolvedObject = result.Data.Items.Single(item => item.LayoutAssetId == objectAssetId);
        Assert.Equal(3, resolvedObject.Files.Count);
        Assert.NotNull(resolvedObject.PrimaryModel);
        Assert.NotNull(resolvedObject.PrimaryTexture);
        Assert.NotNull(resolvedObject.PrimaryPreview);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_Customer_FiltersInternalOnlyFiles()
    {
        var layoutAssetId = Guid.Parse("11111111-1111-1111-1111-111111111112");
        var document = CreateDocument("64fb8f0f2a98f67b1c000009");
        document.Objects[0].ObjectType = "LAYOUT_ASSET";
        document.Objects[0].LayoutAssetId = layoutAssetId;
        document.Objects[0].LayoutAssetType = "STAIR";
        document.Objects[0].ProductVersionId = null;

        var layoutAssets = new FakeLayoutAssetRepository();
        layoutAssets.Assets[layoutAssetId] = new LayoutAsset
        {
            LayoutAssetId = layoutAssetId,
            AssetCode = "STAIR-02",
            AssetName = "Customer stair",
            AssetType = LayoutAssetType.STAIR,
            Status = LayoutAssetStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var projectFiles = new FakeProjectFileRepository();
        projectFiles.CatalogFilesByReferenceId[layoutAssetId] =
        [
            CreateLayoutAssetCatalogFile(layoutAssetId, FileType.MODEL_3D, FileVisibility.STAFF_ONLY, isPrimary: true),
            CreateLayoutAssetCatalogFile(layoutAssetId, FileType.TEXTURE, FileVisibility.CUSTOMER_VISIBLE, isPrimary: true)
        ];

        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id!, ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = document },
            projectFiles: projectFiles,
            layoutAssets: layoutAssets);

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [layoutAssetId] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(200, result.Status);
        var item = Assert.Single(result.Data!.Items);
        Assert.Single(item.Files);
        Assert.Null(item.PrimaryModel);
        Assert.NotNull(item.PrimaryTexture);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_WhenBlueprintLayoutMissing_ResolvesObjectAssetsOnly()
    {
        var layoutAssetId = Guid.Parse("22222222-2222-2222-2222-222222222223");
        var document = CreateDocument("64fb8f0f2a98f67b1c000010");
        document.BlueprintLayout = null;
        document.Objects[0].ObjectType = "LAYOUT_ASSET";
        document.Objects[0].LayoutAssetId = layoutAssetId;
        document.Objects[0].LayoutAssetType = "STAIR";
        document.Objects[0].ProductVersionId = null;

        var layoutAssets = new FakeLayoutAssetRepository();
        layoutAssets.Assets[layoutAssetId] = new LayoutAsset
        {
            LayoutAssetId = layoutAssetId,
            AssetCode = "STAIR-03",
            AssetName = "Object-only asset",
            AssetType = LayoutAssetType.STAIR,
            Status = LayoutAssetStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(document.Id!, ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository { DocumentById = document },
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: layoutAssets);

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [layoutAssetId] },
            DesignerId,
            "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.Equal(layoutAssetId, Assert.Single(result.Data!.Items).LayoutAssetId);
    }

    [Fact]
    public async Task ResolveLayoutAssetsAsync_WhenMongoDocumentMissing_ReturnsAssetNotInScene()
    {
        var service = CreateService(
            new FakeSqlSceneRepository { Context = CreateContext(mongoSceneId: "missing-doc", status: ProposalStatus.PUBLISHED) },
            new FakeSceneDocumentRepository(),
            projectFiles: new FakeProjectFileRepository(),
            layoutAssets: new FakeLayoutAssetRepository());

        var result = await service.ResolveLayoutAssetsAsync(
            SceneId,
            new ResolveRoomPlannerLayoutAssetsRequestDto { LayoutAssetIds = [Guid.NewGuid()] },
            CustomerId,
            "CUSTOMER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_LAYOUT_ASSET_NOT_IN_SCENE", result.ErrorCode);
    }

    private static CatalogFileReadModel CreateLayoutAssetCatalogFile(
        Guid layoutAssetId,
        FileType fileType,
        FileVisibility visibility,
        bool isPrimary = false) =>
        new()
        {
            ReferenceId = layoutAssetId,
            FileId = Guid.NewGuid(),
            FileLinkId = Guid.NewGuid(),
            FileType = fileType,
            FileUrl = $"https://cdn.example.com/{fileType.ToString().ToLowerInvariant()}",
            MimeType = "application/octet-stream",
            Visibility = visibility,
            OriginalFileName = $"{fileType}.bin",
            Status = FileStatus.ACTIVE,
            IsPrimary = isPrimary
        };

    private sealed class FakeLayoutAssetRepository : ILayoutAssetRepository
    {
        public Dictionary<Guid, LayoutAsset> Assets { get; } = [];

        public Task<LayoutAsset?> GetByIdAsync(Guid layoutAssetId, CancellationToken cancellationToken = default)
        {
            Assets.TryGetValue(layoutAssetId, out var asset);
            return Task.FromResult<LayoutAsset?>(asset);
        }

        public Task AddAsync(LayoutAsset layoutAsset, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LayoutAsset?> GetForUpdateAsync(Guid layoutAssetId, CancellationToken cancellationToken = default) => GetByIdAsync(layoutAssetId, cancellationToken);
        public Task<bool> AssetCodeExistsAsync(string normalizedAssetCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> AssetCodeExistsExceptAsync(string normalizedAssetCode, Guid layoutAssetId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<LayoutAsset>> GetPagedAsync(LayoutAssetType? assetType, LayoutAssetStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LayoutAsset>>([]);
        public Task<int> CountAsync(LayoutAssetType? assetType, LayoutAssetStatus? status, string? search, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private static RoomPlannerSceneService CreateService(
        FakeSqlSceneRepository sql,
        FakeSceneDocumentRepository documents,
        FurniSpace.Infrastructure.Persistence.IUnitOfWork? unitOfWork = null,
        IProductVersionRepository? productVersions = null,
        IProjectFileRepository? projectFiles = null,
        ILayoutAssetRepository? layoutAssets = null) =>
        new(sql, documents, unitOfWork ?? TestUnitOfWork.Instance, productVersions, projectFiles, layoutAssets);

    private static RoomPlannerSceneContextReadModel CreateContext(
        string? mongoSceneId = "64fb8f0f2a98f67b1c000000",
        ProposalStatus status = ProposalStatus.DRAFT) =>
        new()
        {
            SceneId = SceneId,
            ProposalId = ProposalId,
            ProjectId = ProjectId,
            SceneType = ProposalSceneType.ROOM_PLANNER,
            SceneAreas =
            [
                CreateSceneArea()
            ],
            MongoSceneId = mongoSceneId,
            ProposalStatus = status,
            CustomerId = CustomerId,
            AssignedSalesId = SalesId,
            AssignedDesignerId = DesignerId
        };

    private static RoomPlannerScenePayloadDto CreateSaveRequest() =>
        new()
        {
            SchemaVersion = 3,
            EditorVersion = "ROOM_PLANNER_BABYLON_V1",
            Unit = "meter",
            BlueprintLayout = new RoomPlannerBlueprintLayoutDocument
            {
                Id = "blueprint-01",
                Name = "Main blueprint",
                Unit = "meter",
                Floors =
                [
                    new RoomPlannerBlueprintFloorDocument
                    {
                        Id = "floor-01",
                        ProjectAreaId = ProjectAreaId,
                        Name = "Main cafe area",
                        LevelIndex = 0,
                        FloorHeight = 3,
                        Points =
                        [
                            new RoomPlannerPoint2Document { PointId = "p1", X = 0, Y = 0 },
                            new RoomPlannerPoint2Document { PointId = "p2", X = 5, Y = 0 }
                        ],
                        Walls =
                        [
                            new RoomPlannerWallDocument
                            {
                                WallId = "w1",
                                StartPointId = "p1",
                                EndPointId = "p2",
                                Height = 3,
                                Thickness = 0.1m,
                                Visible = true,
                                Style = new RoomPlannerStyleDocument
                                {
                                    MaterialId = "wall-base",
                                    Color = "#D8D2C5"
                                }
                            }
                        ],
                        Doors =
                        [
                            new RoomPlannerOpeningDocument
                            {
                                OpeningId = "door-1",
                                Type = "DOOR",
                                WallId = "w1",
                                Offset = 1.4m,
                                Width = 0.8m,
                                Height = 2.1m,
                                SwingDirection = "IN_LEFT",
                                IsOpen = true
                            }
                        ],
                        Windows =
                        [
                            new RoomPlannerOpeningDocument
                            {
                                OpeningId = "window-1",
                                Type = "WINDOW",
                                WallId = "w1",
                                Offset = 2.5m,
                                Width = 1.1m,
                                Height = 1.2m,
                                SillHeight = 0.9m
                            }
                        ]
                    }
                ]
            },
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "object-01",
                    FloorId = "floor-01",
                    ProductVersionId = ProductVersionId,
                    ProductModelId = "product-model-01",
                    ProposalItemId = Guid.NewGuid(),
                    Transform = new RoomPlannerTransformDocument(),
                    Placement = new RoomPlannerPlacementDocument { Mode = "FLOOR", HeightOffset = 0 },
                    ModelSnapshot = new RoomPlannerModelSnapshotDocument
                    {
                        ModelFileId = Guid.NewGuid(),
                        Format = "GLB",
                        ModelUrlSnapshot = "https://storage.test/model.glb"
                    }
                }
            ],
            Camera = new RoomPlannerCameraDocument { Mode = "ORBIT", Zoom = 1 },
            Lighting = new RoomPlannerLightingDocument { Preset = "DEFAULT", AmbientIntensity = 0.8m },
            Validation = new RoomPlannerValidationDocument { Status = "VALID" },
            EditorState = new RoomPlannerEditorStateDocument { ActiveTool = "SELECT", ViewMode = "THREE_D" }
        };

    private static void SetFloorRectangle(
        RoomPlannerBlueprintFloorDocument floor,
        decimal minX,
        decimal minZ,
        decimal width,
        decimal length)
    {
        var maxX = minX + width;
        var maxZ = minZ + length;
        floor.Points =
        [
            new RoomPlannerPoint2Document { PointId = "p1", X = minX, Z = minZ },
            new RoomPlannerPoint2Document { PointId = "p2", X = maxX, Z = minZ },
            new RoomPlannerPoint2Document { PointId = "p3", X = maxX, Z = maxZ },
            new RoomPlannerPoint2Document { PointId = "p4", X = minX, Z = maxZ }
        ];
        floor.Walls =
        [
            CreateBlueprintWall("w1", "p1", "p2"),
            CreateBlueprintWall("w2", "p2", "p3"),
            CreateBlueprintWall("w3", "p3", "p4"),
            CreateBlueprintWall("w4", "p4", "p1")
        ];
    }

    private static RoomPlannerWallDocument CreateBlueprintWall(
        string wallId,
        string startPointId,
        string endPointId) =>
        new()
        {
            WallId = wallId,
            StartPointId = startPointId,
            EndPointId = endPointId,
            Height = 3m,
            Thickness = 0.1m,
            Visible = true
        };

    private static RoomPlannerSceneDocument CreateDocument(string id)
    {
        var request = CreateSaveRequest();
        return new RoomPlannerSceneDocument
        {
            Id = id,
            SchemaVersion = request.SchemaVersion,
            EditorVersion = request.EditorVersion,
            SqlSceneId = SceneId,
            ProposalId = ProposalId,
            ProjectId = ProjectId,
            Unit = request.Unit,
            SceneLinks = new RoomPlannerSceneLinksDocument { ProjectAreaIds = [ProjectAreaId] },
            BlueprintLayout = request.BlueprintLayout,
            Objects = request.Objects,
            Camera = request.Camera,
            Lighting = request.Lighting,
            Validation = request.Validation,
            EditorState = request.EditorState,
            Metadata = new RoomPlannerMetadataDocument { UpdatedAt = DateTime.UtcNow }
        };
    }

    private static ProposalSceneAreaReadModel CreateSceneArea(Guid? projectId = null) =>
        new()
        {
            ProposalSceneAreaId = Guid.NewGuid(),
            SceneId = SceneId,
            ProjectAreaId = ProjectAreaId,
            ProjectId = projectId ?? ProjectId,
            AreaName = "Main cafe area",
            SortOrder = 0
        };

    private static ProposalSceneAreaReadModel CreateSceneAreaWithLayout(
        decimal width,
        decimal length,
        decimal height,
        bool isSpecialLayout) =>
        new()
        {
            ProposalSceneAreaId = Guid.NewGuid(),
            SceneId = SceneId,
            ProjectAreaId = ProjectAreaId,
            ProjectId = ProjectId,
            AreaName = "Main cafe area",
            IsSpecialLayout = isSpecialLayout,
            Width = width,
            Length = length,
            Height = height,
            AreaSqm = width * length,
            SortOrder = 0
        };

    private static FileMetadataReadModel CreateModelFileMetadata(Guid fileId) =>
        new()
        {
            FileId = fileId,
            FileType = FileType.MODEL_3D,
            Status = FileStatus.ACTIVE,
            OriginalFileName = "model.glb"
        };

    private static void ApplyInvalidBlueprintScenario(RoomPlannerScenePayloadDto request, string scenario)
    {
        switch (scenario)
        {
            case "duplicate-floor-id":
                request.BlueprintLayout!.Floors.Add(new RoomPlannerBlueprintFloorDocument
                {
                    Id = "floor-01",
                    ProjectAreaId = Guid.NewGuid()
                });
                break;
            case "empty-floors":
                request.BlueprintLayout!.Floors.Clear();
                break;
            case "missing-mapped-floor":
                request.BlueprintLayout!.Floors.Clear();
                request.BlueprintLayout.Floors.Add(new RoomPlannerBlueprintFloorDocument
                {
                    Id = "floor-01",
                    ProjectAreaId = Guid.NewGuid()
                });
                break;
            case "unmapped-floor":
                request.BlueprintLayout!.Floors[0].ProjectAreaId = Guid.NewGuid();
                break;
            case "unit-mismatch":
                request.BlueprintLayout!.Unit = "cm";
                break;
            case "invalid-wall-point":
                request.BlueprintLayout!.Floors[0].Walls[0].EndPointId = "missing-point";
                break;
            case "invalid-opening-wall":
                request.BlueprintLayout!.Floors[0].Doors[0].WallId = "missing-wall";
                break;
            case "duplicate-point-id":
                request.BlueprintLayout!.Floors[0].Points.Add(new RoomPlannerPoint2Document { PointId = "P1" });
                break;
            case "duplicate-wall-id":
                request.BlueprintLayout!.Floors[0].Walls.Add(new RoomPlannerWallDocument
                {
                    WallId = "W1",
                    StartPointId = "p1",
                    EndPointId = "p2"
                });
                break;
            case "duplicate-object-id":
                request.Objects.Add(new RoomPlannerObjectDocument
                {
                    ObjectId = "OBJECT-01",
                    FloorId = "floor-01",
                    ProductVersionId = ProductVersionId,
                    Transform = new RoomPlannerTransformDocument()
                });
                break;
            case "blank-object-id":
                request.Objects[0].ObjectId = " ";
                break;
            case "blank-floor-id":
                request.BlueprintLayout!.Floors[0].Id = " ";
                break;
            case "duplicate-project-area":
                request.BlueprintLayout!.Floors.Add(new RoomPlannerBlueprintFloorDocument
                {
                    Id = "floor-02",
                    ProjectAreaId = ProjectAreaId
                });
                break;
            case "partial-wall-point":
                request.BlueprintLayout!.Floors[0].Walls[0].EndPointId = null;
                break;
            case "duplicate-opening-id":
                request.BlueprintLayout!.Floors[0].Openings.Add(new RoomPlannerOpeningDocument
                {
                    OpeningId = "door-1",
                    Type = "OPENING",
                    WallId = "w1"
                });
                break;
            case "blank-point-id":
                request.BlueprintLayout!.Floors[0].Points[0].PointId = " ";
                break;
            case "blank-wall-id":
                request.BlueprintLayout!.Floors[0].Walls[0].WallId = " ";
                break;
            case "blank-opening-id":
                request.BlueprintLayout!.Floors[0].Doors[0].OpeningId = " ";
                break;
            case "invalid-window-wall":
                request.BlueprintLayout!.Floors[0].Windows[0].WallId = "missing-wall";
                break;
            case "invalid-openings-wall":
                request.BlueprintLayout!.Floors[0].Openings.Add(new RoomPlannerOpeningDocument
                {
                    OpeningId = "opening-1",
                    Type = "OPENING",
                    WallId = "missing-wall"
                });
                break;
            case "blank-object-floor":
                request.Objects[0].FloorId = " ";
                break;
            case "empty-product-version":
                request.Objects[0].ProductVersionId = Guid.Empty;
                request.Objects[0].ModelSnapshot = null;
                break;
        }
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

    private sealed class FakeProductVersionRepository : IProductVersionRepository
    {
        public HashSet<Guid> ValidProductVersionIds { get; } = [];
        public int GetValidDetailsCallCount { get; private set; }
        public IReadOnlyList<Guid> LastRequestedIds { get; private set; } = [];
        public int LastReturnedCount { get; private set; }

        public Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            GetValidDetailsCallCount++;
            LastRequestedIds = productVersionIds.ToList();
            var details = productVersionIds
                .Where(ValidProductVersionIds.Contains)
                .Select(productVersionId => new ProductVersionDetailReadModel
                {
                    ProductVersionId = productVersionId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Cafe Chair",
                    VersionCode = "CHR-001",
                    VersionName = "Cafe Chair",
                    Status = ProductStatus.ACTIVE,
                    IsProjectSpecific = ValidProductVersionIds.Contains(productVersionId)
                })
                .ToList();
            LastReturnedCount = details.Count;
            return Task.FromResult<IReadOnlyList<ProductVersionDetailReadModel>>(details);
        }

        public IQueryable<ProductVersion> Query() => Array.Empty<ProductVersion>().AsQueryable();
        public Task<ProductVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersion?>(null);
        public Task<IReadOnlyList<ProductVersion>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductVersion>>([]);
        public Task AddAsync(ProductVersion entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<ProductVersion> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(ProductVersion entity) { }
        public void Remove(ProductVersion entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> VersionCodeExistsAsync(string versionCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProductVersionDetailReadModel?> GetPublicDetailAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<ProductVersionDetailReadModel?>(null);
        public Task SetDefaultAsync(ProductVersion productVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountProjectSpecificByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyDictionary<Guid, decimal?>> GetDefaultTaxRatesByIdsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, decimal?>>(new Dictionary<Guid, decimal?>());
    }

    private sealed class FakeProjectFileRepository : IProjectFileRepository
    {
        public Dictionary<Guid, IReadOnlyList<FileLink>> FileLinksByFileId { get; } = [];
        public Dictionary<Guid, FileMetadataReadModel> FileMetadataByFileId { get; } = [];

        public Task<IReadOnlyList<FileLink>> GetFileLinkEntitiesByFileIdAsync(
            Guid fileId,
            CancellationToken cancellationToken = default)
        {
            FileLinksByFileId.TryGetValue(fileId, out var links);
            return Task.FromResult(links ?? []);
        }

        public IQueryable<StoredFile> Query() => Array.Empty<StoredFile>().AsQueryable();
        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<StoredFile?>(null);
        public Task<IReadOnlyList<StoredFile>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task AddAsync(StoredFile entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<StoredFile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(StoredFile entity) { }
        public void Remove(StoredFile entity) { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<ProjectFileAccessReadModel?> GetProjectAccessAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<ProjectFileAccessReadModel?> GetReferenceProjectAccessAsync(string referenceType, Guid referenceId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileAccessReadModel?>(null);
        public Task<string?> GetAccountRoleNameAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task AddFileLinkAsync(FileLink fileLink, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<FileMetadataReadModel?> GetFileMetadataAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            FileMetadataByFileId.TryGetValue(fileId, out var metadata);
            return Task.FromResult<FileMetadataReadModel?>(metadata);
        }
        public Task<FileReferencePageReadModel> GetFilesByReferenceAsync(FileReferenceQueryReadModel query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FileReferencePageReadModel { Items = [], Total = 0 });
        public Task<FileLinkReadModel?> GetFileLinkAsync(Guid fileLinkId, CancellationToken cancellationToken = default) => Task.FromResult<FileLinkReadModel?>(null);
        public void RemoveFileLinks(IEnumerable<FileLink> fileLinks) { }
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(
            string referenceType,
            IReadOnlyList<Guid> referenceIds,
            bool customerVisibleOnly,
            CancellationToken cancellationToken = default)
        {
            var files = referenceIds
                .SelectMany(referenceId => CatalogFilesByReferenceId.GetValueOrDefault(referenceId, []))
                .Where(file => !customerVisibleOnly || file.Visibility == FileVisibility.CUSTOMER_VISIBLE)
                .ToList();
            return Task.FromResult<IReadOnlyList<CatalogFileReadModel>>(files);
        }

        public Dictionary<Guid, List<CatalogFileReadModel>> CatalogFilesByReferenceId { get; } = [];
        public Task<int> CountProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<ProductPreviewImageReadModel>> GetProductPreviewFilesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductPreviewImageReadModel>>([]);
        public Task<ProductPreviewImageReadModel?> GetProductPreviewFileAsync(Guid productId, Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProductPreviewImageReadModel?>(null);
        public Task<IReadOnlyList<FileLink>> GetProductPreviewFileLinkEntitiesAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<int> CountProductVersionPreviewFilesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<FileLink>> GetProductVersionPreviewFileLinkEntitiesAsync(Guid productVersionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileLink>>([]);
        public Task<ProjectFileSearchIndexItemReadModel?> GetSearchIndexItemAsync(Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectFileSearchIndexItemReadModel?>(null);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> GetSearchIndexPageAsync(int page, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<IReadOnlyList<ProjectFileSearchIndexItemReadModel>> SearchByProjectAsync(Guid projectId, string query, int page, int limit, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectFileSearchIndexItemReadModel>>([]);
        public Task<int> CountSearchByProjectAsync(Guid projectId, string query, bool customerVisibleOnly, Guid? customerAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> HasProjectFileWithTypesAsync(Guid projectId, IReadOnlyCollection<FileType> fileTypes, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<ProjectLinkedFileReadModel?> GetProjectLinkedActiveFileAsync(Guid projectId, Guid fileId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectLinkedFileReadModel?>(null);
    }

    private sealed class FakeSceneDocumentRepository : FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository
    {
        public string SavedId { get; set; } = "64fb8f0f2a98f67b1c000099";
        public RoomPlannerSceneDocument? UpsertedDocument { get; private set; }
        public RoomPlannerSceneDocument? DocumentById { get; set; }
        public RoomPlannerSceneDocument? DocumentBySqlSceneId { get; set; }
        public bool ThrowOnUpsert { get; set; }
        public bool ThrowOnGet { get; set; }

        public Task<RoomPlannerSceneDocument?> GetByIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnGet)
            {
                throw new InvalidOperationException("Mongo load failed.");
            }

            return Task.FromResult(DocumentById);
        }

        public Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnGet)
            {
                throw new InvalidOperationException("Mongo load failed.");
            }

            return Task.FromResult(DocumentBySqlSceneId);
        }

        public Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
            RoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnUpsert)
            {
                throw new InvalidOperationException("Mongo save failed.");
            }

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
