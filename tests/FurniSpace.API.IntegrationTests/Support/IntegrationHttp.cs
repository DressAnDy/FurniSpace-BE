using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FurniSpace.API.IntegrationTests.Authentication;

namespace FurniSpace.API.IntegrationTests.Support;

public static class IntegrationHttp
{
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static HttpRequestMessage Authenticated(
        HttpMethod method,
        string path,
        Guid userId,
        string role,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(TestAuthenticationHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(TestAuthenticationHandler.RoleHeader, role);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    public static HttpRequestMessage AuthenticatedJson<T>(
        HttpMethod method,
        string path,
        Guid userId,
        string role,
        T body) =>
        Authenticated(method, path, userId, role, JsonContent.Create(body, options: JsonOptions));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
