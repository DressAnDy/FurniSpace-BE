using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FurniSpace.API.IntegrationTests.Authentication;
using FurniSpace.API.IntegrationTests.Fixtures;

namespace FurniSpace.API.IntegrationTests.Pipeline;

[Collection(ApiIntegrationCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Core")]
public sealed class ApiPipelineIntegrationTests : IAsyncLifetime
{
    private readonly ApiIntegrationFixture _fixture;

    public ApiPipelineIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Root_ReturnsSwaggerUiSmokeResponse()
    {
        var response = await _fixture.Client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Swagger UI", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Request_WithValidCorrelationId_EchoesHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/categories");
        request.Headers.Add("X-Correlation-ID", "integration-request-001");

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("integration-request-001", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task CreateCategory_WithMalformedJson_ReturnsValidationResponse()
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/categories", "ADMIN");
        request.Content = new StringContent("{\"categoryName\":", Encoding.UTF8, "application/json");

        var response = await _fixture.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"status\":400", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"errors\":", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCategory_WithNonAdminRole_ReturnsForbidden()
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/categories", "CUSTOMER");
        request.Content = new StringContent(
            "{\"categoryName\":\"Workspace\"}",
            Encoding.UTF8,
            "application/json");

        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path, string role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserIdHeader, Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RoleHeader, role);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }
}
