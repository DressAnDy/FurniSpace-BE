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
    }
}
