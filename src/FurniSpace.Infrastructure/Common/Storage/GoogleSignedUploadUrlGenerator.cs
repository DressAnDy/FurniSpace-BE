using Google.Cloud.Storage.V1;

namespace FurniSpace.Infrastructure.Common.Storage;

internal sealed class GoogleSignedUploadUrlGenerator : ISignedUploadUrlGenerator
{
    private readonly UrlSigner _urlSigner;

    public GoogleSignedUploadUrlGenerator(UrlSigner urlSigner)
    {
        _urlSigner = urlSigner;
    }

    public string Sign(UrlSigner.RequestTemplate template, UrlSigner.Options options) =>
        _urlSigner.Sign(template, options);
}
