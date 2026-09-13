namespace FurniSpace.Application.Common.Storage;

public interface IFileUploadValidator
{
    FileUploadValidationResult Validate(IFileUploadPayload payload);
}
