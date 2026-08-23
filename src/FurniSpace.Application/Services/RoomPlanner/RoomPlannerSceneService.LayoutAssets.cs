using System.Text.Json;
using FurniSpace.Application.Common;
using FurniSpace.Application.Common.LayoutAssets;
using FurniSpace.Application.Common.Storage;
using FurniSpace.Application.DTOs.RoomPlanner;
using FurniSpace.Application.DTOs.RoomPlannerDocuments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using FurniSpace.Infrastructure.ReadModels.Products;
using FurniSpace.Infrastructure.Repositories.IRepository;
using static FurniSpace.Application.Constants.RoomPlanner.RoomPlannerSceneServiceConstants;

namespace FurniSpace.Application.Services.RoomPlanner;

public sealed partial class RoomPlannerSceneService
{
    private static readonly HashSet<string> LayoutAssetObjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        LayoutAssetObjectType,
        StructuralAssetObjectType,
        DecorativeAssetObjectType
    };

    private static readonly HashSet<string> AllowedFloorOpeningTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        FloorOpeningTypeStair,
        FloorOpeningTypeVoid,
        FloorOpeningTypeServiceShaft
    };

    private static readonly HashSet<LayoutAssetType> StructuralLayoutAssetTypes =
    [
        LayoutAssetType.STAIR,
        LayoutAssetType.COLUMN,
        LayoutAssetType.BEAM
    ];

    private readonly ILayoutAssetRepository? _layoutAssets;

    private static Error? ValidateObjectContracts(IReadOnlyList<RoomPlannerObjectDocument> objects)
    {
        foreach (var sceneObject in objects)
        {
            var objectType = NormalizeIdentifier(sceneObject.ObjectType);
            if (string.IsNullOrWhiteSpace(objectType))
            {
                objectType = FurnitureObjectType;
            }

            if (IsLayoutAssetObjectType(objectType))
            {
                var layoutAssetError = ValidateLayoutAssetObject(sceneObject);
                if (layoutAssetError is not null)
                {
                    return layoutAssetError;
                }

                continue;
            }

            if (IsFurnitureObjectType(objectType))
            {
                if (!HasProductVersionId(sceneObject))
                {
                    return Error.BadRequest(
                        ProductVersionNotFoundCode,
                        "Scene object product version id is required.");
                }

                continue;
            }

            return Error.BadRequest(
                RoomPlannerObjectTypeInvalidCode,
                "Scene object type is invalid.");
        }

        return null;
    }

    private static Error? ValidateLayoutAssetObject(RoomPlannerObjectDocument sceneObject)
    {
        if (!HasLayoutAssetId(sceneObject))
        {
            return Error.BadRequest(
                LayoutAssetNotFoundCode,
                "Layout asset id is required for non-commercial scene objects.");
        }

        if (HasProductVersionId(sceneObject) || sceneObject.ProposalItemId.HasValue)
        {
            return Error.BadRequest(
                RoomPlannerLayoutAssetForbiddenCode,
                "Commercial product fields are not allowed on layout asset scene objects.");
        }

        if (string.IsNullOrWhiteSpace(sceneObject.LayoutAssetType))
        {
            return Error.BadRequest(
                RoomPlannerObjectTypeInvalidCode,
                "Layout asset type is required for layout asset scene objects.");
        }

        return null;
    }

    private static Error? ValidateFloorOpenings(RoomPlannerScenePayloadDto request)
    {
        var blueprintLayout = request.BlueprintLayout!;
        var levels = ExtractFloorOpeningLevels(blueprintLayout.Metadata);
        if (levels.Count == 0)
        {
            return null;
        }

        var floorsByLevelIndex = blueprintLayout.Floors
            .OrderBy(floor => floor.LevelIndex ?? int.MaxValue)
            .ThenBy(floor => floor.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            var openings = levels[levelIndex];
            if (openings.Count == 0)
            {
                continue;
            }

            var floor = levelIndex < floorsByLevelIndex.Count
                ? floorsByLevelIndex[levelIndex]
                : floorsByLevelIndex.FirstOrDefault();

            var openingIds = openings
                .Select(opening => NormalizeIdentifier(opening.Id))
                .ToList();

            if (openingIds.Any(string.IsNullOrWhiteSpace))
            {
                return Error.BadRequest(
                    RoomPlannerFloorOpeningInvalidCode,
                    "Floor opening id is required.");
            }

            if (ContainsDuplicateIdentifiers(openingIds))
            {
                return Error.BadRequest(
                    RoomPlannerFloorOpeningDuplicateCode,
                    "Floor opening ids must be unique within the same level.");
            }

            foreach (var opening in openings)
            {
                var openingError = ValidateSingleFloorOpening(opening, floor);
                if (openingError is not null)
                {
                    return openingError;
                }
            }
        }

        return null;
    }

    private static Error? ValidateSingleFloorOpening(
        RoomPlannerFloorOpeningDocument opening,
        RoomPlannerBlueprintFloorDocument? floor)
    {
        var openingType = NormalizeIdentifier(opening.Type);
        if (string.IsNullOrWhiteSpace(openingType) || !AllowedFloorOpeningTypes.Contains(openingType))
        {
            return Error.BadRequest(
                RoomPlannerFloorOpeningInvalidCode,
                "Floor opening type is invalid.");
        }

        if (opening.Width is not > 0m || opening.Depth is not > 0m)
        {
            return Error.BadRequest(
                RoomPlannerFloorOpeningInvalidCode,
                "Floor opening width and depth must be greater than zero.");
        }

        if (opening.Position is null)
        {
            return Error.BadRequest(
                RoomPlannerFloorOpeningInvalidCode,
                "Floor opening position is required.");
        }

        if (floor is not null && !IsFloorOpeningWithinBounds(opening, floor))
        {
            return Error.BadRequest(
                RoomPlannerFloorOpeningOutOfBoundsCode,
                "Floor opening is outside the valid floor boundary.");
        }

        return null;
    }

    private static bool IsFloorOpeningWithinBounds(
        RoomPlannerFloorOpeningDocument opening,
        RoomPlannerBlueprintFloorDocument floor)
    {
        var bounds = GetFloorBounds(floor);
        if (bounds is null)
        {
            return true;
        }

        var (minX, maxX, minZ, maxZ) = bounds.Value;
        var halfWidth = opening.Width!.Value / 2m;
        var halfDepth = opening.Depth!.Value / 2m;
        var centerX = opening.Position!.X;
        var centerZ = opening.Position.Z;

        return centerX - halfWidth >= minX &&
            centerX + halfWidth <= maxX &&
            centerZ - halfDepth >= minZ &&
            centerZ + halfDepth <= maxZ;
    }

    private static (decimal MinX, decimal MaxX, decimal MinZ, decimal MaxZ)? GetFloorBounds(
        RoomPlannerBlueprintFloorDocument floor)
    {
        if (floor.Points.Count == 0)
        {
            return null;
        }

        return (
            floor.Points.Min(point => point.X),
            floor.Points.Max(point => point.X),
            floor.Points.Min(point => point.Z),
            floor.Points.Max(point => point.Z));
    }

    private static List<List<RoomPlannerFloorOpeningDocument>> ExtractFloorOpeningLevels(
        Dictionary<string, object?>? metadata)
    {
        if (metadata is null ||
            !TryGetObjectProperty(metadata, "building", out var buildingValue) ||
            buildingValue is not Dictionary<string, object?> building)
        {
            return [];
        }

        if (!TryGetArrayProperty(building, "levels", out var levels))
        {
            return [];
        }

        var result = new List<List<RoomPlannerFloorOpeningDocument>>();
        foreach (var levelValue in levels)
        {
            if (levelValue is not Dictionary<string, object?> levelDictionary)
            {
                result.Add([]);
                continue;
            }

            if (!TryGetArrayProperty(levelDictionary, "floorOpenings", out var openingValues))
            {
                result.Add([]);
                continue;
            }

            var openings = openingValues
                .Select(TryParseFloorOpening)
                .Where(opening => opening is not null)
                .Cast<RoomPlannerFloorOpeningDocument>()
                .ToList();
            result.Add(openings);
        }

        return result;
    }

    private static RoomPlannerFloorOpeningDocument? TryParseFloorOpening(object? value)
    {
        if (value is not Dictionary<string, object?> dictionary)
        {
            return null;
        }

        var opening = new RoomPlannerFloorOpeningDocument
        {
            Id = ReadString(dictionary, "id") ?? string.Empty,
            Type = ReadString(dictionary, "type") ?? string.Empty,
            Label = ReadString(dictionary, "label"),
            Width = ReadDecimal(dictionary, "width"),
            Depth = ReadDecimal(dictionary, "depth"),
            LayoutAssetId = ReadGuid(dictionary, "layoutAssetId")
        };

        if (TryGetObjectProperty(dictionary, "position", out var positionValue) &&
            positionValue is Dictionary<string, object?> positionDictionary)
        {
            opening.Position = new RoomPlannerPoint2Document
            {
                X = ReadDecimal(positionDictionary, "x") ?? 0m,
                Z = ReadDecimal(positionDictionary, "z") ?? 0m
            };
        }

        return opening;
    }

    private async Task<Error?> ValidateLayoutAssetReferencesAsync(
        RoomPlannerScenePayloadDto request,
        CancellationToken cancellationToken)
    {
        if (_layoutAssets is null)
        {
            return null;
        }

        var layoutAssetIds = CollectReferencedLayoutAssetIds(request);
        if (layoutAssetIds.Count == 0)
        {
            return null;
        }

        var assetsById = await LoadLayoutAssetsAsync(_layoutAssets, layoutAssetIds, cancellationToken);

        var sceneObjectError = ValidateSceneObjectLayoutAssets(request.Objects, assetsById);
        if (sceneObjectError is not null)
        {
            return sceneObjectError;
        }

        var blueprintError = ValidateBlueprintLayoutAssets(request.BlueprintLayout!.Floors, assetsById);
        if (blueprintError is not null)
        {
            return blueprintError;
        }

        return ValidateFloorOpeningLayoutAssets(
            ExtractFloorOpeningLevels(request.BlueprintLayout.Metadata),
            assetsById);
    }

    private static Error? ValidateSceneObjectLayoutAssets(
        IReadOnlyList<RoomPlannerObjectDocument> objects,
        Dictionary<Guid, LayoutAsset> assetsById)
    {
        foreach (var sceneObject in objects.Where(IsLayoutAssetObject))
        {
            var assetError = ValidateActiveLayoutAsset(
                assetsById,
                sceneObject.LayoutAssetId!.Value,
                ResolveExpectedLayoutAssetTypesForObject(sceneObject));
            if (assetError is not null)
            {
                return assetError;
            }
        }

        return null;
    }

    private static Error? ValidateBlueprintLayoutAssets(
        IReadOnlyList<RoomPlannerBlueprintFloorDocument> floors,
        Dictionary<Guid, LayoutAsset> assetsById)
    {
        foreach (var floor in floors)
        {
            foreach (var wall in floor.Walls)
            {
                if (wall.Style?.LayoutAssetId is not Guid wallMaterialId)
                {
                    continue;
                }

                var wallError = ValidateActiveLayoutAsset(
                    assetsById,
                    wallMaterialId,
                    [LayoutAssetType.WALL_MATERIAL]);
                if (wallError is not null)
                {
                    return wallError;
                }
            }

            if (floor.FloorStyle?.LayoutAssetId is not Guid floorMaterialId)
            {
                continue;
            }

            var floorError = ValidateActiveLayoutAsset(
                assetsById,
                floorMaterialId,
                [LayoutAssetType.FLOOR_MATERIAL]);
            if (floorError is not null)
            {
                return floorError;
            }
        }

        return null;
    }

    private static Error? ValidateFloorOpeningLayoutAssets(
        IReadOnlyList<IReadOnlyList<RoomPlannerFloorOpeningDocument>> levels,
        Dictionary<Guid, LayoutAsset> assetsById)
    {
        foreach (var openings in levels)
        {
            foreach (var opening in openings)
            {
                if (opening.LayoutAssetId is not Guid openingAssetId)
                {
                    continue;
                }

                var expectedTypes = string.Equals(
                    opening.Type,
                    FloorOpeningTypeStair,
                    StringComparison.OrdinalIgnoreCase)
                    ? new[] { LayoutAssetType.STAIR }
                    : StructuralLayoutAssetTypes.ToArray();

                var openingError = ValidateActiveLayoutAsset(assetsById, openingAssetId, expectedTypes);
                if (openingError is not null)
                {
                    return openingError;
                }
            }
        }

        return null;
    }

    private static HashSet<Guid> CollectReferencedLayoutAssetIds(RoomPlannerScenePayloadDto request)
    {
        var layoutAssetIds = new HashSet<Guid>();
        CollectObjectLayoutAssetIds(request.Objects, layoutAssetIds);
        CollectBlueprintLayoutAssetIds(request.BlueprintLayout!.Floors, layoutAssetIds);
        CollectFloorOpeningLayoutAssetIds(
            ExtractFloorOpeningLevels(request.BlueprintLayout.Metadata),
            layoutAssetIds);
        return layoutAssetIds;
    }

    private static void CollectObjectLayoutAssetIds(
        IReadOnlyList<RoomPlannerObjectDocument> objects,
        HashSet<Guid> layoutAssetIds)
    {
        foreach (var sceneObject in objects)
        {
            if (sceneObject.LayoutAssetId is Guid objectAssetId && objectAssetId != Guid.Empty)
            {
                layoutAssetIds.Add(objectAssetId);
            }
        }
    }

    private static void CollectBlueprintLayoutAssetIds(
        IReadOnlyList<RoomPlannerBlueprintFloorDocument> floors,
        HashSet<Guid> layoutAssetIds)
    {
        foreach (var floor in floors)
        {
            foreach (var wall in floor.Walls)
            {
                if (wall.Style?.LayoutAssetId is Guid wallAssetId && wallAssetId != Guid.Empty)
                {
                    layoutAssetIds.Add(wallAssetId);
                }
            }

            if (floor.FloorStyle?.LayoutAssetId is Guid floorAssetId && floorAssetId != Guid.Empty)
            {
                layoutAssetIds.Add(floorAssetId);
            }
        }
    }

    private static void CollectFloorOpeningLayoutAssetIds(
        IReadOnlyList<IReadOnlyList<RoomPlannerFloorOpeningDocument>> levels,
        HashSet<Guid> layoutAssetIds)
    {
        foreach (var openings in levels)
        {
            foreach (var opening in openings)
            {
                if (opening.LayoutAssetId is Guid openingAssetId && openingAssetId != Guid.Empty)
                {
                    layoutAssetIds.Add(openingAssetId);
                }
            }
        }
    }

    private static async Task<Dictionary<Guid, LayoutAsset>> LoadLayoutAssetsAsync(
        ILayoutAssetRepository layoutAssets,
        IEnumerable<Guid> layoutAssetIds,
        CancellationToken cancellationToken)
    {
        var assetsById = new Dictionary<Guid, LayoutAsset>();
        foreach (var layoutAssetId in layoutAssetIds)
        {
            var asset = await layoutAssets.GetByIdAsync(layoutAssetId, cancellationToken);
            if (asset is not null)
            {
                assetsById[layoutAssetId] = asset;
            }
        }

        return assetsById;
    }

    private static Error? ValidateActiveLayoutAsset(
        Dictionary<Guid, LayoutAsset> assetsById,
        Guid layoutAssetId,
        LayoutAssetType[] expectedTypes)
    {
        if (!assetsById.TryGetValue(layoutAssetId, out var asset))
        {
            return Error.BadRequest(LayoutAssetNotFoundCode, "Referenced layout asset was not found.");
        }

        if (asset.Status != LayoutAssetStatus.ACTIVE)
        {
            return Error.BadRequest(
                LayoutAssetInactiveCode,
                "Referenced layout asset must be active.");
        }

        if (expectedTypes.Length > 0 && !expectedTypes.Contains(asset.AssetType))
        {
            return Error.BadRequest(
                RoomPlannerSurfaceMaterialInvalidCode,
                "Referenced layout asset type is invalid for this surface or opening.");
        }

        return null;
    }

    private static LayoutAssetType[] ResolveExpectedLayoutAssetTypesForObject(RoomPlannerObjectDocument sceneObject)
    {
        var objectType = NormalizeIdentifier(sceneObject.ObjectType);
        if (string.Equals(objectType, DecorativeAssetObjectType, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                LayoutAssetType.DECORATIVE_WALL,
                LayoutAssetType.DECORATIVE_FLOOR,
                LayoutAssetType.DECORATIVE_OBJECT,
                LayoutAssetType.OTHER
            ];
        }

        if (string.Equals(objectType, StructuralAssetObjectType, StringComparison.OrdinalIgnoreCase))
        {
            return StructuralLayoutAssetTypes.ToArray();
        }

        return Enum.GetValues<LayoutAssetType>();
    }

    private async Task AppendInactiveLayoutAssetWarningsAsync(
        RoomPlannerSceneResponseDto response,
        CancellationToken cancellationToken)
    {
        if (_layoutAssets is null)
        {
            return;
        }

        response.Validation ??= new RoomPlannerValidationDocument();
        response.Validation.Warnings ??= [];

        var referencedAssets = CollectReferencedLayoutAssetIds(new RoomPlannerScenePayloadDto
        {
            BlueprintLayout = response.BlueprintLayout,
            Objects = response.Objects
        });

        if (referencedAssets.Count == 0)
        {
            return;
        }

        var assetsById = await LoadLayoutAssetsAsync(_layoutAssets, referencedAssets, cancellationToken);
        foreach (var sceneObject in response.Objects.Where(objectDocument =>
                     objectDocument.LayoutAssetId is Guid layoutAssetId &&
                     layoutAssetId != Guid.Empty &&
                     (!assetsById.TryGetValue(layoutAssetId, out var asset) ||
                      asset.Status != LayoutAssetStatus.ACTIVE)))
        {
            response.Validation.Warnings.Add(new RoomPlannerValidationIssueDocument
            {
                Code = LayoutAssetInactiveCode,
                Severity = "WARNING",
                ObjectId = sceneObject.ObjectId,
                LayoutAssetId = sceneObject.LayoutAssetId,
                Message = "Layout asset is inactive but preserved for existing scene rendering."
            });
        }
    }

    private static bool IsFurnitureObject(RoomPlannerObjectDocument sceneObject) =>
        IsFurnitureObjectType(NormalizeIdentifier(sceneObject.ObjectType));

    private static bool IsLayoutAssetObject(RoomPlannerObjectDocument sceneObject) =>
        IsLayoutAssetObjectType(NormalizeIdentifier(sceneObject.ObjectType));

    private static bool IsFurnitureObjectType(string objectType) =>
        string.IsNullOrWhiteSpace(objectType) ||
        string.Equals(objectType, FurnitureObjectType, StringComparison.OrdinalIgnoreCase);

    private static bool IsLayoutAssetObjectType(string objectType) =>
        LayoutAssetObjectTypes.Contains(objectType);

    private static bool HasProductVersionId(RoomPlannerObjectDocument sceneObject) =>
        sceneObject.ProductVersionId is Guid productVersionId && productVersionId != Guid.Empty;

    private static bool HasLayoutAssetId(RoomPlannerObjectDocument sceneObject) =>
        sceneObject.LayoutAssetId is Guid layoutAssetId && layoutAssetId != Guid.Empty;

    private static bool TryGetObjectProperty(
        Dictionary<string, object?> source,
        string propertyName,
        out object? value)
    {
        if (source.TryGetValue(propertyName, out value))
        {
            return true;
        }

        var match = source.FirstOrDefault(pair =>
            string.Equals(pair.Key, propertyName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(match.Key))
        {
            value = match.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetArrayProperty(
        Dictionary<string, object?> source,
        string propertyName,
        out List<object?> values)
    {
        values = [];
        if (!TryGetObjectProperty(source, propertyName, out var rawValue) || rawValue is null)
        {
            return false;
        }

        switch (rawValue)
        {
            case List<object?> objectList:
                values = objectList;
                return true;
            case object[] objectArray:
                values = objectArray.Cast<object?>().ToList();
                return true;
            case JsonElement { ValueKind: JsonValueKind.Array } jsonArray:
                values = jsonArray.EnumerateArray().Select(element => (object?)element).ToList();
                return true;
            default:
                return false;
        }
    }

    private static string? ReadString(Dictionary<string, object?> source, string propertyName)
    {
        if (!TryGetObjectProperty(source, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } jsonString => jsonString.GetString(),
            _ => value.ToString()
        };
    }

    private static decimal? ReadDecimal(Dictionary<string, object?> source, string propertyName)
    {
        if (!TryGetObjectProperty(source, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => (decimal)doubleValue,
            float floatValue => (decimal)floatValue,
            int intValue => intValue,
            long longValue => longValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number =>
                jsonElement.TryGetDecimal(out var decimalFromJson)
                    ? decimalFromJson
                    : null,
            _ => null
        };
    }

    private static Guid? ReadGuid(Dictionary<string, object?> source, string propertyName)
    {
        var text = ReadString(source, propertyName);
        return Guid.TryParse(text, out var parsedGuid) ? parsedGuid : null;
    }

    private async Task<HashSet<Guid>> GetSceneLayoutAssetIdsAsync(
        Infrastructure.ReadModels.RoomPlanner.RoomPlannerSceneContextReadModel context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.MongoSceneId))
        {
            return [];
        }

        var document = await _sceneDocuments.GetByIdAsync(context.MongoSceneId, cancellationToken);
        if (document is null)
        {
            return [];
        }

        if (document.BlueprintLayout is null)
        {
            var objectAssetIds = new HashSet<Guid>();
            CollectObjectLayoutAssetIds(document.Objects, objectAssetIds);
            return objectAssetIds;
        }

        return CollectReferencedLayoutAssetIds(new RoomPlannerScenePayloadDto
        {
            Objects = document.Objects,
            BlueprintLayout = document.BlueprintLayout
        });
    }

    private static RoomPlannerResolvedLayoutAssetDto ToResolvedLayoutAssetDto(
        LayoutAsset asset,
        IReadOnlyList<CatalogFileReadModel> files,
        bool customerVisibleOnly)
    {
        var visibleFiles = CatalogFileOrdering.FilterVisible(files, customerVisibleOnly)
            .Where(file => !string.IsNullOrWhiteSpace(file.FileUrl))
            .ToList();

        return new RoomPlannerResolvedLayoutAssetDto
        {
            LayoutAssetId = asset.LayoutAssetId,
            AssetCode = asset.AssetCode,
            AssetName = asset.AssetName,
            AssetType = asset.AssetType,
            Description = asset.Description,
            Status = asset.Status,
            Files = LayoutAssetFileSummaryHelper.ToFileDtos(visibleFiles),
            PrimaryModel = LayoutAssetFileSummaryHelper.PickPrimary(visibleFiles, FileType.MODEL_3D),
            PrimaryTexture = LayoutAssetFileSummaryHelper.PickPrimary(visibleFiles, FileType.TEXTURE),
            PrimaryPreview = LayoutAssetFileSummaryHelper.PickPrimaryPreview(visibleFiles)
        };
    }
}
