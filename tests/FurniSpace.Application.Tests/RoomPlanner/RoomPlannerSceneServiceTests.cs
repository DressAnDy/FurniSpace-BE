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
    public async Task SaveSceneAsync_PublishedProposal_ReturnsProposalNotEditable()
    {
        var sql = new FakeSqlSceneRepository { Context = CreateContext(status: ProposalStatus.PUBLISHED) };
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(sql, documents);

        var result = await service.SaveSceneAsync(SceneId, CreateSaveRequest(), DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("PROPOSAL_NOT_EDITABLE", result.ErrorCode);
        Assert.Null(documents.UpsertedDocument);
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

    private static RoomPlannerSceneService CreateService(
        FakeSqlSceneRepository sql,
        FakeSceneDocumentRepository documents,
        FurniSpace.Infrastructure.Persistence.IUnitOfWork? unitOfWork = null,
        IProductVersionRepository? productVersions = null,
        IProjectFileRepository? projectFiles = null) =>
        new(sql, documents, unitOfWork ?? TestUnitOfWork.Instance, productVersions, projectFiles);

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

        public Task<IReadOnlyList<ProductVersionDetailReadModel>> GetValidDetailsAsync(
            IReadOnlyCollection<Guid> productVersionIds,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var details = productVersionIds
                .Where(ValidProductVersionIds.Contains)
                .Select(productVersionId => new ProductVersionDetailReadModel
                {
                    ProductVersionId = productVersionId,
                    ProductId = Guid.NewGuid(),
                    VersionName = "Cafe Chair",
                    Status = ProductStatus.ACTIVE
                })
                .ToList();
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
        public Task<IReadOnlyList<CatalogFileReadModel>> GetCatalogFilesByReferencesAsync(string referenceType, IReadOnlyList<Guid> referenceIds, bool customerVisibleOnly, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CatalogFileReadModel>>([]);
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
    }

    private sealed class FakeSceneDocumentRepository : FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository
    {
        public string SavedId { get; set; } = "64fb8f0f2a98f67b1c000099";
        public RoomPlannerSceneDocument? UpsertedDocument { get; private set; }
        public RoomPlannerSceneDocument? DocumentById { get; set; }
        public RoomPlannerSceneDocument? DocumentBySqlSceneId { get; set; }
        public bool ThrowOnUpsert { get; set; }

        public Task<RoomPlannerSceneDocument?> GetByIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DocumentById);

        public Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DocumentBySqlSceneId);

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
