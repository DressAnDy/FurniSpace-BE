using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Application.DTOs.ProjectFiles;

namespace FurniSpace.Application.Interfaces.MeasurementImages;

public interface IMeasurementImageService
{
    Task<ServiceResult<MeasurementImageUploadResponseDto>> UploadMeasurementImageAsync(
        Guid scheduleId,
        Guid currentUserId,
        UploadMeasurementImageRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetProjectMeasurementImagesAsync(
        Guid projectId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetScheduleMeasurementImagesAsync(
        Guid scheduleId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeasurementImageGalleryResponseDto>> GetProjectAreaMeasurementImagesAsync(
        Guid projectAreaId,
        Guid currentUserId,
        MeasurementImageGalleryQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeasurementImageAreaLinkResponseDto>> LinkMeasurementImageToAreaAsync(
        Guid projectAreaId,
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MeasurementImageAreaLinkResponseDto>> UnlinkMeasurementImageFromAreaAsync(
        Guid projectAreaId,
        Guid fileId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
