using Google.Cloud.Storage.V1;

namespace FurniSpace.Infrastructure.Common.Storage;

public interface ISignedUploadUrlGenerator
{
    string Sign(UrlSigner.RequestTemplate template, UrlSigner.Options options);
}
