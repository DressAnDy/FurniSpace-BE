namespace FurniSpace.Application.Common.Storage;

public interface IFileUploadValidator
{
    FileUploadValidationResult Validate(IFileUploadPayload payload);

    FileUploadValidationResult ValidateMetadata(
        string originalFileName,
        string contentType,
        long fileSizeBytes);
}
