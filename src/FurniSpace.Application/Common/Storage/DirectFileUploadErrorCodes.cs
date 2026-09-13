namespace FurniSpace.Application.Common.Storage;

public static class DirectFileUploadErrorCodes
{
    public const string UploadNotFound = "FILE_UPLOAD_NOT_FOUND";
    public const string UploadNotPending = "FILE_UPLOAD_NOT_PENDING";
    public const string UploadForbidden = "FILE_UPLOAD_FORBIDDEN";
    public const string UploadObjectMissing = "FILE_UPLOAD_OBJECT_MISSING";
    public const string UploadSizeMismatch = "FILE_UPLOAD_SIZE_MISMATCH";
    public const string UploadContentTypeMismatch = "FILE_UPLOAD_CONTENT_TYPE_MISMATCH";
}
