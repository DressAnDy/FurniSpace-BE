using System.Net;

namespace FurniSpace.Infrastructure.Interfaces;

public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(
        string message,
        HttpStatusCode? statusCode = null,
        string? providerMessage = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ProviderMessage = providerMessage;
    }

    public HttpStatusCode? StatusCode { get; }
    public string? ProviderMessage { get; }
}
