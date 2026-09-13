namespace FurniSpace.Application.Common.Storage;

public enum FileUploadValidationFailureKind
{
    MissingFile,
    MissingFileName,
    InvalidFileSize,
    FileTooLarge,
    InvalidExtension,
    InvalidMimeType
}
