#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Interfaces.RoomPlanner;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Application.Tests.TestDoubles;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.RoomPlanner;
using FurniSpace.Infrastructure.Repositories.IRepository;
using Xunit;
using RoomPlannerSqlSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerProposalSceneRepository;
using ApplicationRoomPlannerSceneRepository = FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository;

namespace FurniSpace.Application.Tests.RoomPlanner;

public sealed class RoomPlannerSceneServiceLayoutAssetTests
{
    private static readonly Guid SceneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProposalId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DesignerId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProjectAreaId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid LayoutAssetId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid WallMaterialAssetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid FloorMaterialAssetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task SaveSceneAsync_WithValidLayoutAssetObject_SavesSuccessfully()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        RegisterActiveAsset(layoutAssets, LayoutAssetId, LayoutAssetType.STAIR);
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(documents, layoutAssets);
        var request = CreateLayoutAssetSaveRequest();

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        Assert.NotNull(documents.UpsertedDocument);
        Assert.Equal(LayoutAssetId, documents.UpsertedDocument!.Objects[0].LayoutAssetId);
        Assert.Null(documents.UpsertedDocument.Objects[0].ProductVersionId);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenLayoutAssetMissingId_ReturnsLayoutAssetNotFound()
    {
        var service = CreateService(new FakeSceneDocumentRepository(), CreateLayoutAssetRepository());
        var request = CreateLayoutAssetSaveRequest();
        request.Objects[0].LayoutAssetId = null;

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("LAYOUT_ASSET_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenLayoutAssetHasCommercialFields_ReturnsForbidden()
    {
        var service = CreateService(new FakeSceneDocumentRepository(), CreateLayoutAssetRepository());
        var request = CreateLayoutAssetSaveRequest();
        request.Objects[0].ProductVersionId = Guid.NewGuid();

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_LAYOUT_ASSET_FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenObjectTypeInvalid_ReturnsObjectTypeInvalid()
    {
        var service = CreateService(new FakeSceneDocumentRepository(), CreateLayoutAssetRepository());
        var request = CreateLayoutAssetSaveRequest();
        request.Objects[0].ObjectType = "UNKNOWN";

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_OBJECT_TYPE_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenLayoutAssetInactive_ReturnsLayoutAssetInactive()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        layoutAssets.Assets[LayoutAssetId] = CreateAsset(LayoutAssetId, LayoutAssetType.STAIR, LayoutAssetStatus.INACTIVE);
        var service = CreateService(new FakeSceneDocumentRepository(), layoutAssets);
        var request = CreateLayoutAssetSaveRequest();

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("LAYOUT_ASSET_INACTIVE", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenWallMaterialAssetInvalid_ReturnsSurfaceMaterialInvalid()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        RegisterActiveAsset(layoutAssets, LayoutAssetId, LayoutAssetType.STAIR);
        layoutAssets.Assets[WallMaterialAssetId] = CreateAsset(
            WallMaterialAssetId,
            LayoutAssetType.STAIR,
            LayoutAssetStatus.ACTIVE);
        var service = CreateService(new FakeSceneDocumentRepository(), layoutAssets);
        var request = CreateLayoutAssetSaveRequest();
        request.BlueprintLayout!.Floors[0].Walls[0].Style!.LayoutAssetId = WallMaterialAssetId;

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_SURFACE_MATERIAL_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenFloorOpeningDuplicateId_ReturnsDuplicateError()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        RegisterActiveAsset(layoutAssets, LayoutAssetId, LayoutAssetType.STAIR);
        var service = CreateService(new FakeSceneDocumentRepository(), layoutAssets);
        var request = CreateLayoutAssetSaveRequest();
        request.BlueprintLayout!.Metadata = CreateFloorOpeningMetadata(
            CreateOpening("opening-1", 1m, 0m, 1m, 1m),
            CreateOpening("opening-1", 2m, 0m, 1m, 1m));

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_FLOOR_OPENING_DUPLICATE", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_WhenFloorOpeningOutOfBounds_ReturnsOutOfBoundsError()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        RegisterActiveAsset(layoutAssets, LayoutAssetId, LayoutAssetType.STAIR);
        var service = CreateService(new FakeSceneDocumentRepository(), layoutAssets);
        var request = CreateLayoutAssetSaveRequest();
        request.BlueprintLayout!.Metadata = CreateFloorOpeningMetadata(
            CreateOpening("opening-1", 10m, 10m, 2m, 2m));

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(400, result.Status);
        Assert.Equal("ROOM_PLANNER_FLOOR_OPENING_OUT_OF_BOUNDS", result.ErrorCode);
    }

    [Fact]
    public async Task SaveSceneAsync_PreservesFloorOpeningMetadata()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        RegisterActiveAsset(layoutAssets, LayoutAssetId, LayoutAssetType.STAIR);
        var documents = new FakeSceneDocumentRepository();
        var service = CreateService(documents, layoutAssets);
        var request = CreateLayoutAssetSaveRequest();
        request.BlueprintLayout!.Metadata = CreateFloorOpeningMetadata(
            CreateOpening("opening-1", 1m, 2m, 1.5m, 2.2m, label: "Floor hole"));

        var result = await service.SaveSceneAsync(SceneId, request, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        var metadata = documents.UpsertedDocument!.BlueprintLayout!.Metadata;
        Assert.True(metadata.ContainsKey("building"));
    }

    [Fact]
    public async Task GetSceneAsync_WhenLayoutAssetInactive_ReturnsWarning()
    {
        var layoutAssets = CreateLayoutAssetRepository();
        layoutAssets.Assets[LayoutAssetId] = CreateAsset(LayoutAssetId, LayoutAssetType.STAIR, LayoutAssetStatus.INACTIVE);
        var document = CreateLayoutAssetDocument("mongo-layout-asset");
        var service = CreateService(
            new FakeSceneDocumentRepository { DocumentById = document },
            layoutAssets,
            new FakeSqlSceneRepository { Context = CreateContext(document.Id!) });

        var result = await service.GetSceneAsync(SceneId, DesignerId, "DESIGNER");

        Assert.Equal(200, result.Status);
        var warning = Assert.Single(result.Data!.Validation.Warnings);
        Assert.Equal("LAYOUT_ASSET_INACTIVE", warning.Code);
        Assert.Equal(LayoutAssetId, warning.LayoutAssetId);
    }

    private static RoomPlannerSceneService CreateService(
        FakeSceneDocumentRepository documents,
        FakeLayoutAssetRepository layoutAssets,
        FakeSqlSceneRepository? sql = null) =>
        new(
            sql ?? new FakeSqlSceneRepository { Context = CreateContext() },
            documents,
            TestUnitOfWork.Instance,
            layoutAssets: layoutAssets);

    private static RoomPlannerSceneContextReadModel CreateContext(string? mongoSceneId = "mongo-layout-asset") =>
        new()
        {
            SceneId = SceneId,
            ProposalId = ProposalId,
            ProjectId = ProjectId,
            SceneType = ProposalSceneType.ROOM_PLANNER,
            SceneAreas =
            [
                new()
                {
                    ProposalSceneAreaId = Guid.NewGuid(),
                    SceneId = SceneId,
                    ProjectAreaId = ProjectAreaId,
                    ProjectId = ProjectId,
                    AreaName = "Main area",
                    SortOrder = 0
                }
            ],
            MongoSceneId = mongoSceneId,
            ProposalStatus = ProposalStatus.DRAFT,
            AssignedDesignerId = DesignerId
        };

    private static RoomPlannerScenePayloadDto CreateLayoutAssetSaveRequest()
    {
        var request = new RoomPlannerScenePayloadDto
        {
            SchemaVersion = 3,
            EditorVersion = "ROOM_PLANNER_BABYLON_V1",
            Unit = "meter",
            BlueprintLayout = new RoomPlannerBlueprintLayoutDocument
            {
                Id = "blueprint-01",
                Unit = "meter",
                Floors =
                [
                    new RoomPlannerBlueprintFloorDocument
                    {
                        Id = "floor-01",
                        ProjectAreaId = ProjectAreaId,
                        LevelIndex = 0,
                        Points =
                        [
                            new RoomPlannerPoint2Document { PointId = "p1", X = 0, Z = 0 },
                            new RoomPlannerPoint2Document { PointId = "p2", X = 5, Z = 0 },
                            new RoomPlannerPoint2Document { PointId = "p3", X = 5, Z = 5 },
                            new RoomPlannerPoint2Document { PointId = "p4", X = 0, Z = 5 }
                        ],
                        Walls =
                        [
                            new RoomPlannerWallDocument
                            {
                                WallId = "w1",
                                StartPointId = "p1",
                                EndPointId = "p2",
                                Style = new RoomPlannerStyleDocument()
                            }
                        ]
                    }
                ]
            },
            Objects =
            [
                new RoomPlannerObjectDocument
                {
                    ObjectId = "layout-object-01",
                    FloorId = "floor-01",
                    ObjectType = "LAYOUT_ASSET",
                    LayoutAssetId = LayoutAssetId,
                    LayoutAssetType = "STAIR",
                    Name = "Straight Stair",
                    Transform = new RoomPlannerTransformDocument(),
                    Placement = new RoomPlannerPlacementDocument { Mode = "FLOOR" }
                }
            ]
        };

        request.BlueprintLayout.Floors[0].FloorStyle = new RoomPlannerFloorDocument
        {
            LayoutAssetId = FloorMaterialAssetId,
            MaterialId = "legacy-floor"
        };

        return request;
    }

    private static RoomPlannerSceneDocument CreateLayoutAssetDocument(string id)
    {
        var request = CreateLayoutAssetSaveRequest();
        return new RoomPlannerSceneDocument
        {
            Id = id,
            SchemaVersion = 3,
            SqlSceneId = SceneId,
            ProposalId = ProposalId,
            ProjectId = ProjectId,
            Unit = "meter",
            SceneLinks = new RoomPlannerSceneLinksDocument { ProjectAreaIds = [ProjectAreaId] },
            BlueprintLayout = request.BlueprintLayout,
            Objects = request.Objects,
            Validation = new RoomPlannerValidationDocument(),
            Metadata = new RoomPlannerMetadataDocument { UpdatedAt = DateTime.UtcNow }
        };
    }

    private static Dictionary<string, object?> CreateFloorOpeningMetadata(
        params Dictionary<string, object?>[] openings) =>
        new()
        {
            ["building"] = new Dictionary<string, object?>
            {
                ["levels"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["floorOpenings"] = new List<object?>(openings)
                    }
                }
            }
        };

    private static Dictionary<string, object?> CreateOpening(
        string id,
        decimal x,
        decimal z,
        decimal width,
        decimal depth,
        string? label = null) =>
        new()
        {
            ["id"] = id,
            ["type"] = "STAIR",
            ["label"] = label,
            ["position"] = new Dictionary<string, object?> { ["x"] = x, ["z"] = z },
            ["width"] = width,
            ["depth"] = depth,
            ["layoutAssetId"] = null,
            ["modelSnapshot"] = null
        };

    private static FakeLayoutAssetRepository CreateLayoutAssetRepository()
    {
        var repository = new FakeLayoutAssetRepository();
        RegisterActiveAsset(repository, LayoutAssetId, LayoutAssetType.STAIR);
        RegisterActiveAsset(repository, FloorMaterialAssetId, LayoutAssetType.FLOOR_MATERIAL);
        RegisterActiveAsset(repository, WallMaterialAssetId, LayoutAssetType.WALL_MATERIAL);
        return repository;
    }

    private static void RegisterActiveAsset(
        FakeLayoutAssetRepository repository,
        Guid layoutAssetId,
        LayoutAssetType assetType) =>
        repository.Assets[layoutAssetId] = CreateAsset(layoutAssetId, assetType, LayoutAssetStatus.ACTIVE);

    private static LayoutAsset CreateAsset(
        Guid layoutAssetId,
        LayoutAssetType assetType,
        LayoutAssetStatus status) =>
        new()
        {
            LayoutAssetId = layoutAssetId,
            AssetCode = $"ASSET-{layoutAssetId:N}"[..20],
            AssetName = "Test asset",
            AssetType = assetType,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class FakeSqlSceneRepository : RoomPlannerSqlSceneRepository
    {
        public RoomPlannerSceneContextReadModel? Context { get; set; }

        public Task<RoomPlannerSceneContextReadModel?> GetContextAsync(
            Guid sceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Context);

        public Task UpdateMongoSceneIdAsync(
            Guid sceneId,
            string mongoSceneId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeSceneDocumentRepository
        : FurniSpace.Application.Interfaces.RoomPlanner.IRoomPlannerSceneRepository
    {
        public RoomPlannerSceneDocument? DocumentById { get; set; }
        public RoomPlannerSceneDocument? UpsertedDocument { get; private set; }

        public Task<RoomPlannerSceneDocument?> GetByIdAsync(
            string mongoSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DocumentById);

        public Task<RoomPlannerSceneDocument?> GetBySqlSceneIdAsync(
            Guid sqlSceneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomPlannerSceneDocument?>(null);

        public Task<RoomPlannerSceneDocument> UpsertBySqlSceneIdAsync(
            RoomPlannerSceneDocument document,
            CancellationToken cancellationToken = default)
        {
            document.Id ??= "mongo-layout-asset";
            UpsertedDocument = document;
            return Task.FromResult(document);
        }

        public Task<bool> DeleteBySqlSceneIdAsync(Guid sqlSceneId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeLayoutAssetRepository : ILayoutAssetRepository
    {
        public Dictionary<Guid, LayoutAsset> Assets { get; } = [];

        public Task<LayoutAsset?> GetByIdAsync(Guid layoutAssetId, CancellationToken cancellationToken = default)
        {
            Assets.TryGetValue(layoutAssetId, out var asset);
            return Task.FromResult(asset);
        }

        public Task AddAsync(LayoutAsset layoutAsset, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LayoutAsset?> GetForUpdateAsync(Guid layoutAssetId, CancellationToken cancellationToken = default) => GetByIdAsync(layoutAssetId, cancellationToken);
        public Task<bool> AssetCodeExistsAsync(string normalizedAssetCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> AssetCodeExistsExceptAsync(string normalizedAssetCode, Guid layoutAssetId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<LayoutAsset>> GetPagedAsync(LayoutAssetType? assetType, LayoutAssetStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LayoutAsset>>([]);
        public Task<int> CountAsync(LayoutAssetType? assetType, LayoutAssetStatus? status, string? search, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
