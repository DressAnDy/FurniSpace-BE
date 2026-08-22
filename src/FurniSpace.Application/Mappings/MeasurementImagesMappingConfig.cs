using FurniSpace.Application.DTOs.MeasurementImages;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class MeasurementImagesMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<MeasurementImageGalleryItemReadModel, MeasurementImageGalleryItemDto>()
            .Map(destination => destination.Url, source => source.FileUrl)
            .Map(
                destination => destination.MeasurementSchedule,
                source => new MeasurementImageScheduleSummaryDto
                {
                    ScheduleId = source.ScheduleId,
                    ScheduledStart = source.ScheduledStart
                })
            .Map(destination => destination.Areas, source => source.Areas.Adapt<List<MeasurementImageAreaSummaryDto>>());

        config.NewConfig<MeasurementImageAreaAssignmentReadModel, MeasurementImageAreaSummaryDto>();
    }
}
