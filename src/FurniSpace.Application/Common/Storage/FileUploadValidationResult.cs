namespace FurniSpace.Application.Common.Storage;

public sealed class FileUploadValidationResult
{
    public bool IsValid => FailureKind is null;

    public FileUploadValidationFailureKind? FailureKind { get; init; }

    public string Message { get; init; } = string.Empty;

    public static FileUploadValidationResult Success()
    {
        return new FileUploadValidationResult();
    }

    public static FileUploadValidationResult Failure(
        FileUploadValidationFailureKind failureKind,
        string message)
    {
        return new FileUploadValidationResult
        {
            FailureKind = failureKind,
            Message = message
        };
    }
}
