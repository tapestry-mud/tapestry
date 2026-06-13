using System.Net;
using System.Net.Http.Json;

namespace Tapestry.Server.Tests.Auth;

[Collection("auth-integration")]
public class AuthEndpointTests : IDisposable
{
    private readonly AuthTestApp _app;
    private readonly HttpClient _client;

    public AuthEndpointTests()
    {
        _app = new AuthTestApp(preAuthEnabled: true);
        _client = _app.CreateClient();
    }

    [Fact]
    public async Task Select_Returns400_WhenSessionTokenMissing()
    {
        var response = await _client.PostAsJsonAsync("/auth/select", new { newCharacter = "Gandalf" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Select_Returns401_WhenSessionTokenInvalid()
    {
        var response = await _client.PostAsJsonAsync("/auth/select", new
        {
            sessionToken = "not-a-real-token",
            newCharacter = "Gandalf"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_Returns400_WhenPasswordBelowFloor()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "test@example.com",
            password = "abc",
            character = "Legolas"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body?["error"]);
        Assert.Contains("6", body!["error"]);
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }
}

[Collection("auth-integration")]
public class AuthGatingTests : IDisposable
{
    private readonly AuthTestApp _app;
    private readonly HttpClient _client;

    public AuthGatingTests()
    {
        _app = new AuthTestApp(preAuthEnabled: false);
        _client = _app.CreateClient();
    }

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/auth/select")]
    [InlineData("/auth/login-by-character")]
    [InlineData("/auth/register")]
    public async Task CredentialEndpoints_Return404_WhenPreAuthDisabled(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfigEndpoint_Returns200_WhenPreAuthDisabled()
    {
        var response = await _client.GetAsync("/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.Dispose();
    }
}
