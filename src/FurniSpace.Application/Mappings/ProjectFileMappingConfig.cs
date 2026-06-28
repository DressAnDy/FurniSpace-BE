using FurniSpace.Application.DTOs.ProjectFiles;
using FurniSpace.Infrastructure.ReadModels.ProjectFiles;
using Mapster;

namespace FurniSpace.Application.Mappings;

public sealed class ProjectFileMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<FileMetadataReadModel, FileDetailResponseDto>()
            .Map(destination => destination.FileName, source => source.StoredFileName)
            .Map(destination => destination.FileSize, source => source.FileSizeBytes)
            .Map(destination => destination.PublicUrl, source => source.FileUrl);

        config.NewConfig<FileMetadataReadModel, FileListItemDto>()
            .Map(destination => destination.FileLinkId, source => source.FileLinkId ?? Guid.Empty)
            .Map(destination => destination.FileSize, source => source.FileSizeBytes)
            .Map(destination => destination.PublicUrl, source => source.FileUrl);
    }
}
