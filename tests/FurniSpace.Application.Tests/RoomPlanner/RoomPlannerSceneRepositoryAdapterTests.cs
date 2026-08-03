#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Application.Services.RoomPlanner;
using FurniSpace.Domain.Enums;
using InfrastructureRoomPlannerSceneRepository = FurniSpace.Infrastructure.Repositories.IRepository.IRoomPlannerSceneRepository;
using InfrastructureRoomPlannerSceneDocument = FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneDocument;
using Xunit;

namespace FurniSpace.Application.Tests.RoomPlanner;

public sealed class RoomPlannerSceneRepositoryAdapterTests
{
    private static readonly JsonSerializerOptions WebJsonSerializerOptions = new(JsonSerializerDefaults.Web);

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
    public async Task UpsertBySqlSceneIdAsync_PreservesBlueprintWallGraphContractFields()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var document = CreateApplicationDocument("app-mongo-id");
        var thumbnailFileId = Guid.NewGuid();
        var modelFileId = Guid.NewGuid();
        document.Layout = new RoomPlannerLayoutDocument
        {
            Type = "BLUEPRINT_WALL_GRAPH",
            IsClosed = true,
            AreaSqFt = 320,
            AreaSqm = 29.73m,
            WallHeight = 9,
            WallThickness = 0.3m,
            FloorMaterialId = "wood-floor",
            WallMaterialId = "wall-base",
            Points =
            [
                new RoomPlannerPoint2Document { PointId = "p1", X = 0, Y = 0 },
                new RoomPlannerPoint2Document { PointId = "p2", X = 16, Y = 0 }
            ],
            Walls =
            [
                new RoomPlannerWallDocument
                {
                    WallId = "w1",
                    StartPointId = "p1",
                    EndPointId = "p2",
                    Height = 9,
                    Thickness = 0.3m,
                    Style = new RoomPlannerStyleDocument
                    {
                        MaterialId = "wall-base",
                        TextureUrlSnapshot = "/materials/wall-paint/wallbase.jpg"
                    }
                }
            ],
            Doors =
            [
                new RoomPlannerOpeningDocument
                {
                    OpeningId = "door-1",
                    WallId = "w1",
                    Type = "DOOR",
                    Offset = 4.5m,
                    Width = 2.5m,
                    Height = 7,
                    SwingDirection = "IN_LEFT",
                    IsOpen = true
                }
            ],
            Windows =
            [
                new RoomPlannerOpeningDocument
                {
                    OpeningId = "window-1",
                    WallId = "w1",
                    Type = "WINDOW",
                    Offset = 8.25m,
                    Width = 3.5m,
                    Height = 4,
                    SillHeight = 3
                }
            ],
            Openings =
            [
                new RoomPlannerOpeningDocument
                {
                    OpeningId = "opening-1",
                    WallId = "w1",
                    Type = "OPENING",
                    Offset = 5.25m,
                    Width = 2,
                    Height = 7,
                    FloorOffset = 0,
                    Locked = true
                }
            ],
            Floor = new RoomPlannerFloorDocument
            {
                MaterialId = "wood-floor",
                TextureUrlSnapshot = "/materials/flooring/woodfloor.jpg"
            }
        };
        document.Objects[0].ProductModelId = "catalog-model-01";
        document.Objects[0].Placement = new RoomPlannerPlacementDocument
        {
            Mode = "FLOOR",
            HeightOffset = 0,
            MountedWallId = "w1"
        };
        document.Objects[0].VisualSnapshot = new RoomPlannerVisualSnapshotDocument
        {
            ThumbnailFileId = thumbnailFileId,
            ThumbnailUrlSnapshot = "https://cdn.example.com/thumb.png"
        };
        document.Objects[0].ModelSnapshot = new RoomPlannerModelSnapshotDocument
        {
            ModelFileId = modelFileId,
            Format = "GLB",
            ModelUrlSnapshot = "https://cdn.example.com/model.glb"
        };

        var result = await adapter.UpsertBySqlSceneIdAsync(document);

        Assert.NotNull(inner.UpsertedDocument);
        Assert.Equal("BLUEPRINT_WALL_GRAPH", inner.UpsertedDocument.Layout!.Type);
        Assert.Equal("p1", inner.UpsertedDocument.Layout.Points[0].PointId);
        Assert.Equal(0, inner.UpsertedDocument.Layout.Points[0].Y);
        Assert.Equal("p1", inner.UpsertedDocument.Layout.Walls[0].StartPointId);
        Assert.Single(inner.UpsertedDocument.Layout.Doors);
        Assert.Single(inner.UpsertedDocument.Layout.Windows);
        Assert.Single(inner.UpsertedDocument.Layout.Openings);
        Assert.Equal(4.5m, inner.UpsertedDocument.Layout.Doors[0].Offset);
        Assert.Equal("w1", inner.UpsertedDocument.Layout.Doors[0].WallId);
        Assert.True(inner.UpsertedDocument.Layout.Doors[0].IsOpen);
        Assert.Equal(8.25m, inner.UpsertedDocument.Layout.Windows[0].Offset);
        Assert.Equal(3, inner.UpsertedDocument.Layout.Windows[0].SillHeight);
        Assert.Equal(5.25m, inner.UpsertedDocument.Layout.Openings[0].Offset);
        Assert.Equal(0, inner.UpsertedDocument.Layout.Openings[0].FloorOffset);
        Assert.True(inner.UpsertedDocument.Layout.Openings[0].Locked);
        Assert.Equal("wood-floor", inner.UpsertedDocument.Layout.Floor.MaterialId);
        Assert.Equal("catalog-model-01", inner.UpsertedDocument.Objects[0].ProductModelId);
        Assert.Equal("FLOOR", inner.UpsertedDocument.Objects[0].Placement.Mode);
        Assert.Equal("w1", inner.UpsertedDocument.Objects[0].Placement.MountedWallId);
        Assert.Equal("https://cdn.example.com/thumb.png", inner.UpsertedDocument.Objects[0].VisualSnapshot!.ThumbnailUrlSnapshot);
        Assert.Equal("https://cdn.example.com/model.glb", inner.UpsertedDocument.Objects[0].ModelSnapshot!.ModelUrlSnapshot);
        Assert.Equal("BLUEPRINT_WALL_GRAPH", result.Layout!.Type);
        Assert.Equal("p2", result.Layout.Points[1].PointId);
        Assert.Equal(4.5m, result.Layout.Doors[0].Offset);
        Assert.Equal(8.25m, result.Layout.Windows[0].Offset);
        Assert.Equal(5.25m, result.Layout.Openings[0].Offset);
        Assert.Equal("w1", result.Objects[0].Placement.MountedWallId);
        Assert.Equal(modelFileId, result.Objects[0].ModelSnapshot!.ModelFileId);
    }

    [Fact]
    public async Task UpsertBySqlSceneIdAsync_PreservesMultiFloorBlueprintDocumentFields()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var projectAreaId = Guid.NewGuid();
        var document = CreateApplicationDocument("app-mongo-id");
        document.SchemaVersion = 3;
        document.ProjectAreaId = null;
        document.Layout = null;
        document.SceneLinks = new RoomPlannerSceneLinksDocument
        {
            ProjectAreaIds = [projectAreaId]
        };
        document.BlueprintLayout = new RoomPlannerBlueprintLayoutDocument
        {
            Id = "blueprint-01",
            Name = "Main blueprint",
            Unit = "meter",
            Scale = 1,
            Metadata = new Dictionary<string, object?> { ["source"] = CreateJsonElement("\"client\"") },
            Floors =
            [
                new RoomPlannerBlueprintFloorDocument
                {
                    Id = "floor-01",
                    ProjectAreaId = projectAreaId,
                    Name = "Ground floor",
                    Points =
                    [
                        new RoomPlannerPoint2Document { PointId = "p1", X = 0, Y = 0 },
                        new RoomPlannerPoint2Document { PointId = "p2", X = 4, Y = 0 }
                    ],
                    Walls =
                    [
                        new RoomPlannerWallDocument
                        {
                            WallId = "w1",
                            StartPointId = "p1",
                            EndPointId = "p2"
                        }
                    ],
                    Doors =
                    [
                        new RoomPlannerOpeningDocument
                        {
                            OpeningId = "door-1",
                            Type = "DOOR",
                            WallId = "w1",
                            Offset = 1.2m
                        }
                    ],
                    Rooms = [new Dictionary<string, object?> { ["roomId"] = CreateJsonElement("\"room-01\"") }]
                }
            ]
        };
        document.Objects[0].FloorId = "floor-01";
        document.Objects[0].ProposalItemId = Guid.NewGuid();
        document.Objects[0].ModelSnapshot = new RoomPlannerModelSnapshotDocument
        {
            ModelFileId = Guid.NewGuid(),
            Format = "GLB"
        };

        var result = await adapter.UpsertBySqlSceneIdAsync(document);

        Assert.NotNull(inner.UpsertedDocument);
        Assert.Null(inner.UpsertedDocument.ProjectAreaId);
        Assert.Null(inner.UpsertedDocument.Layout);
        Assert.Equal(3, inner.UpsertedDocument.SchemaVersion);
        Assert.Equal(projectAreaId, inner.UpsertedDocument.SceneLinks!.ProjectAreaIds[0]);
        Assert.Equal("floor-01", inner.UpsertedDocument.BlueprintLayout!.Floors[0].Id);
        Assert.Equal("p1", inner.UpsertedDocument.BlueprintLayout.Floors[0].Points[0].PointId);
        Assert.Equal("w1", inner.UpsertedDocument.BlueprintLayout.Floors[0].Doors[0].WallId);
        Assert.Equal(1.2m, inner.UpsertedDocument.BlueprintLayout.Floors[0].Doors[0].Offset);
        Assert.Equal("floor-01", inner.UpsertedDocument.Objects[0].FloorId);
        Assert.Equal("GLB", inner.UpsertedDocument.Objects[0].ModelSnapshot!.Format);
        Assert.Null(result.ProjectAreaId);
        Assert.Null(result.Layout);
        Assert.Equal(projectAreaId, result.SceneLinks!.ProjectAreaIds[0]);
        Assert.True(result.SceneLinks.ContainsProjectArea(projectAreaId));
        Assert.Equal("floor-01", result.BlueprintLayout!.Floors[0].Id);
        Assert.Same(result.BlueprintLayout.Floors[0], result.BlueprintLayout.FindFloor("FLOOR-01"));
        Assert.True(result.BlueprintLayout.Floors[0].ContainsWall("W1"));
        Assert.Equal("floor-01", result.Objects[0].FloorId);
        var json = JsonSerializer.Serialize(result, WebJsonSerializerOptions);
        Assert.DoesNotContain("$numberDecimal", json);
    }

    [Fact]
    public async Task UpsertBySqlSceneIdAsync_WhenDynamicCollectionsNull_NormalizesToEmpty()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var document = CreateApplicationDocument("app-mongo-id");
        document.Objects = null!;
        document.Layers = null!;
        document.Camera = null!;
        document.Lighting = null!;
        document.Validation = null!;
        document.SceneLinks = null!;
        document.Metadata = null!;
        document.BlueprintLayout = new RoomPlannerBlueprintLayoutDocument
        {
            Id = "blueprint-01",
            Unit = "meter",
            Metadata = null!,
            Floors = null!
        };
        document.BlueprintLayout.Floors =
        [
            new RoomPlannerBlueprintFloorDocument
            {
                Id = "floor-01",
                ProjectAreaId = Guid.NewGuid(),
                Points = null!,
                Walls = null!,
                Doors = null!,
                Windows = null!,
                Openings = null!,
                Rooms = null!,
                Slabs = null!,
                Stairs = null!,
                Balconies = null!,
                Yards = null!,
                Columns = null!,
                Beams = null!
            }
        ];
        document.EditorState = new RoomPlannerEditorStateDocument { SnapSettings = null! };

        var result = await adapter.UpsertBySqlSceneIdAsync(document);

        Assert.NotNull(inner.UpsertedDocument);
        Assert.Empty(inner.UpsertedDocument.Objects);
        Assert.Empty(inner.UpsertedDocument.Layers);
        Assert.NotNull(inner.UpsertedDocument.Camera);
        Assert.NotNull(inner.UpsertedDocument.Lighting);
        Assert.Empty(inner.UpsertedDocument.Lighting.CustomLights);
        Assert.NotNull(inner.UpsertedDocument.Validation);
        Assert.NotNull(inner.UpsertedDocument.SceneLinks);
        Assert.NotNull(inner.UpsertedDocument.Metadata);
        Assert.Empty(inner.UpsertedDocument.BlueprintLayout!.Metadata);
        Assert.Empty(inner.UpsertedDocument.BlueprintLayout.Floors[0].Points);
        Assert.Empty(inner.UpsertedDocument.BlueprintLayout.Floors[0].Rooms);
        Assert.Empty(result.EditorState!.SnapSettings);
    }

    [Fact]
    public async Task UpsertBySqlSceneIdAsync_WhenBlueprintLayoutNull_SkipsFloorNormalization()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var document = CreateApplicationDocument("app-mongo-id");
        document.BlueprintLayout = null;
        document.EditorState = null;

        await adapter.UpsertBySqlSceneIdAsync(document);

        Assert.NotNull(inner.UpsertedDocument);
        Assert.Null(inner.UpsertedDocument.BlueprintLayout);
        Assert.Null(inner.UpsertedDocument.EditorState);
    }

    [Fact]
    public async Task GetBySqlSceneIdAsync_WhenInnerReturnsNull_ReturnsNull()
    {
        var adapter = new RoomPlannerSceneRepositoryAdapter(new FakeInfrastructureRoomPlannerSceneRepository());

        var result = await adapter.GetBySqlSceneIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertBySqlSceneIdAsync_NormalizesAllJsonElementValueKinds()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var document = CreateApplicationDocument("app-mongo-id");
        document.Objects[0].MaterialOverrides = new Dictionary<string, object?>
        {
            ["text"] = CreateJsonElement("\"hello\""),
            ["int"] = CreateJsonElement("42"),
            ["float"] = CreateJsonElement("3.14"),
            ["flagTrue"] = CreateJsonElement("true"),
            ["flagFalse"] = CreateJsonElement("false"),
            ["missing"] = CreateJsonElement("null"),
            ["plain"] = "already-normalized",
            ["nested"] = CreateJsonElement("{\"items\":[1,null,false]}")
        };

        await adapter.UpsertBySqlSceneIdAsync(document);

        var overrides = inner.UpsertedDocument!.Objects[0].MaterialOverrides;
        Assert.Equal("hello", overrides["text"]);
        Assert.Equal(42L, Convert.ToInt64(overrides["int"]));
        Assert.Equal(3.14d, Convert.ToDouble(overrides["float"]), 3);
        Assert.Equal(true, overrides["flagTrue"]);
        Assert.Equal(false, overrides["flagFalse"]);
        Assert.Null(overrides["missing"]);
        Assert.Equal("already-normalized", overrides["plain"]);
        Assert.IsType<Dictionary<string, object?>>(overrides["nested"]);
    }

    [Fact]
    public async Task UpsertBySqlSceneIdAsync_NormalizesJsonElementDynamicValues()
    {
        var inner = new FakeInfrastructureRoomPlannerSceneRepository();
        var adapter = new RoomPlannerSceneRepositoryAdapter(inner);
        var document = CreateApplicationDocument("app-mongo-id");
        document.Objects[0].MaterialOverrides = new Dictionary<string, object?>
        {
            ["seatColor"] = CreateJsonElement("\"#7A4A24\""),
            ["nested"] = CreateJsonElement("{\"enabled\":true,\"levels\":[1,2]}")
        };
        document.Lighting.CustomLights =
        [
            new Dictionary<string, object?>
            {
                ["type"] = CreateJsonElement("\"POINT\""),
                ["intensity"] = CreateJsonElement("0.7")
            }
        ];
        document.EditorState = new RoomPlannerEditorStateDocument
        {
            SnapSettings = new Dictionary<string, object?>
            {
                ["snapToGrid"] = CreateJsonElement("true")
            }
        };

        await adapter.UpsertBySqlSceneIdAsync(document);

        Assert.NotNull(inner.UpsertedDocument);
        Assert.IsType<string>(inner.UpsertedDocument.Objects[0].MaterialOverrides["seatColor"]);
        Assert.IsType<Dictionary<string, object?>>(inner.UpsertedDocument.Objects[0].MaterialOverrides["nested"]);
        Assert.IsType<string>(inner.UpsertedDocument.Lighting.CustomLights[0]["type"]);
        Assert.IsType<double>(inner.UpsertedDocument.Lighting.CustomLights[0]["intensity"]);
        Assert.IsType<bool>(inner.UpsertedDocument.EditorState!.SnapSettings["snapToGrid"]);
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

    private static JsonElement CreateJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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
                new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerObjectDocument
                {
                    ObjectId = "object-01",
                    ProductVersionId = Guid.NewGuid(),
                    Transform = new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerTransformDocument()
                }
            ],
            Metadata = new FurniSpace.Infrastructure.Data.Mongo.RoomPlannerSceneMetadataDocument
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
