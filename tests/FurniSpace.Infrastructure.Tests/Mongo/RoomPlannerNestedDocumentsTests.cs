#nullable enable

using System;
using FurniSpace.Infrastructure.Data.Mongo;
using Xunit;

namespace FurniSpace.Infrastructure.Tests.Mongo;

public sealed class RoomPlannerNestedDocumentsTests
{
    [Fact]
    public void NestedDocuments_InitializeDefaults()
    {
        var layout = new RoomPlannerLayoutDocument();
        var wall = new RoomPlannerWallDocument();
        var item = new RoomPlannerObjectDocument();
        var transform = new RoomPlannerTransformDocument();
        var dimensions = new RoomPlannerDimensionsSnapshotDocument();
        var camera = new RoomPlannerCameraDocument();
        var lighting = new RoomPlannerLightingDocument();
        var validation = new RoomPlannerValidationDocument();
        var editor = new RoomPlannerEditorStateDocument();
        var layer = new RoomPlannerLayerDocument();
        var sceneLinks = new RoomPlannerSceneLinksDocument();
        var blueprint = new RoomPlannerBlueprintLayoutDocument();
        var blueprintFloor = new RoomPlannerBlueprintFloorDocument();

        Assert.NotNull(layout.Floor);
        Assert.NotNull(wall.Start);
        Assert.NotNull(wall.End);
        Assert.NotNull(wall.Style);
        Assert.NotNull(item.Transform);
        Assert.NotNull(item.DimensionsSnapshot);
        Assert.Equal(1, transform.Scale.X);
        Assert.Equal(1, transform.Scale.Y);
        Assert.Equal(1, transform.Scale.Z);
        Assert.Equal("cm", dimensions.Unit);
        Assert.Equal("ORBIT", camera.Mode);
        Assert.NotNull(camera.Position);
        Assert.NotNull(camera.Target);
        Assert.Equal("DEFAULT", lighting.Preset);
        Assert.Equal("default", lighting.Environment);
        Assert.Empty(lighting.CustomLights);
        Assert.Equal("NOT_VALIDATED", validation.Status);
        Assert.Empty(validation.Warnings);
        Assert.Empty(validation.Errors);
        Assert.Empty(editor.SnapSettings);
        Assert.True(layer.Visible);
        Assert.False(layer.Locked);
        Assert.Empty(sceneLinks.ProjectAreaIds);
        Assert.Equal("meter", blueprint.Unit);
        Assert.Empty(blueprint.Floors);
        Assert.Empty(blueprint.Metadata);
        Assert.Empty(blueprintFloor.Points);
        Assert.Empty(blueprintFloor.Rooms);
        Assert.Empty(blueprintFloor.Walls);
        Assert.Empty(blueprintFloor.Doors);
        Assert.Empty(blueprintFloor.Windows);
        Assert.Empty(blueprintFloor.Openings);
        Assert.Empty(blueprintFloor.Slabs);
        Assert.Empty(blueprintFloor.Stairs);
        Assert.Empty(blueprintFloor.Balconies);
        Assert.Empty(blueprintFloor.Yards);
        Assert.Empty(blueprintFloor.Columns);
        Assert.Empty(blueprintFloor.Beams);
    }

    [Fact]
    public void NestedDocuments_StoreAssignedValues()
    {
        var textureId = Guid.NewGuid();
        var floor = new RoomPlannerFloorDocument
        {
            Color = "#ffffff",
            MaterialCode = "oak",
            TextureFileId = textureId,
            Rotation = 45,
            Scale = 2
        };
        var style = new RoomPlannerStyleDocument
        {
            Color = "#111111",
            MaterialCode = "walnut",
            TextureFileId = textureId,
            TextureRotation = 90,
            TextureScale = 3
        };
        var visual = new RoomPlannerVisualSnapshotDocument
        {
            Material = "wood",
            Color = "brown",
            Finish = "matte"
        };
        var model = new RoomPlannerModelSnapshotDocument
        {
            ModelFileId = textureId,
            Format = "glb"
        };
        var issue = new RoomPlannerValidationIssueDocument
        {
            Code = "WARN",
            Severity = "warning",
            ObjectId = "chair-1",
            Message = "Too close to wall"
        };
        var item = new RoomPlannerObjectDocument
        {
            FloorId = "floor-01"
        };
        var sceneLinks = new RoomPlannerSceneLinksDocument
        {
            ProjectAreaIds = [textureId]
        };
        var blueprint = new RoomPlannerBlueprintLayoutDocument
        {
            Id = "blueprint-01",
            Name = "Main blueprint",
            Scale = 1,
            NorthDirection = 90,
            Metadata = new() { ["source"] = "test" },
            Floors =
            [
                new RoomPlannerBlueprintFloorDocument
                {
                    Id = "floor-01",
                    ProjectAreaId = textureId,
                    Name = "Ground floor",
                    LevelIndex = 0,
                    Elevation = 0,
                    FloorHeight = 3.2m,
                    SlabThickness = 0.15m
                }
            ]
        };

        Assert.Equal("#ffffff", floor.Color);
        Assert.Equal("oak", floor.MaterialCode);
        Assert.Equal(textureId, floor.TextureFileId);
        Assert.Equal(45, floor.Rotation);
        Assert.Equal(2, floor.Scale);
        Assert.Equal("#111111", style.Color);
        Assert.Equal("walnut", style.MaterialCode);
        Assert.Equal(textureId, style.TextureFileId);
        Assert.Equal(90, style.TextureRotation);
        Assert.Equal(3, style.TextureScale);
        Assert.Equal("wood", visual.Material);
        Assert.Equal("brown", visual.Color);
        Assert.Equal("matte", visual.Finish);
        Assert.Equal(textureId, model.ModelFileId);
        Assert.Equal("glb", model.Format);
        Assert.Equal("WARN", issue.Code);
        Assert.Equal("warning", issue.Severity);
        Assert.Equal("chair-1", issue.ObjectId);
        Assert.Equal("Too close to wall", issue.Message);
        Assert.Equal("floor-01", item.FloorId);
        Assert.Equal(textureId, sceneLinks.ProjectAreaIds[0]);
        Assert.True(sceneLinks.ContainsProjectArea(textureId));
        Assert.Equal("blueprint-01", blueprint.Id);
        Assert.Equal("Main blueprint", blueprint.Name);
        Assert.Equal(1, blueprint.Scale);
        Assert.Equal(90, blueprint.NorthDirection);
        Assert.Equal("test", blueprint.Metadata["source"]);
        Assert.Equal(textureId, blueprint.Floors[0].ProjectAreaId);
        Assert.Equal("Ground floor", blueprint.Floors[0].Name);
        Assert.Equal(0, blueprint.Floors[0].LevelIndex);
        Assert.Equal(0, blueprint.Floors[0].Elevation);
        Assert.Equal(3.2m, blueprint.Floors[0].FloorHeight);
        Assert.Equal(0.15m, blueprint.Floors[0].SlabThickness);
        Assert.Same(blueprint.Floors[0], blueprint.FindFloor("FLOOR-01"));
        blueprint.Floors[0].Walls.Add(new RoomPlannerWallDocument { WallId = "wall-01" });
        Assert.True(blueprint.Floors[0].ContainsWall("WALL-01"));
    }
}
