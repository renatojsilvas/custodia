using System.Net;

namespace Custodia.API.Tests.Integration;

[Collection("api")]
public sealed class ApiKeyStrictEqualityTests(ApiTestFactory factory)
{
    private const string ApiKeyHeader = ApiTestFactory.ApiKeyHeader;
    private const string ProtectedPath = "/_test/result/success";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_WithExactConfiguredApiKey_ShouldReturn200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Add(ApiKeyHeader, ApiTestFactory.ValidApiKey);

        var response = await _client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithStrictPrefixOfConfiguredApiKey_ShouldReturn401()
    {
        var strictPrefix = ApiTestFactory.ValidApiKey[..^1];

        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Add(ApiKeyHeader, strictPrefix);

        var response = await _client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithConfiguredApiKeyPlusExtraSuffix_ShouldReturn401()
    {
        var supersetKey = ApiTestFactory.ValidApiKey + "extra";

        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Add(ApiKeyHeader, supersetKey);

        var response = await _client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithOneCharacterSwappedInTheMiddleOfConfiguredApiKey_ShouldReturn401()
    {
        var middleIndex = ApiTestFactory.ValidApiKey.Length / 2;
        var originalChar = ApiTestFactory.ValidApiKey[middleIndex];
        var swappedChar = originalChar == 'z' ? 'y' : 'z';
        var swappedKey = ApiTestFactory.ValidApiKey[..middleIndex] + swappedChar + ApiTestFactory.ValidApiKey[(middleIndex + 1)..];
        Assert.Equal(ApiTestFactory.ValidApiKey.Length, swappedKey.Length);
        Assert.NotEqual(ApiTestFactory.ValidApiKey, swappedKey);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Add(ApiKeyHeader, swappedKey);

        var response = await _client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithConfiguredApiKeyCaseFlippedOnALetter_ShouldReturn401()
    {
        Assert.Contains(ApiTestFactory.ValidApiKey, c => char.IsLetter(c));
        var letterIndex = ApiTestFactory.ValidApiKey.IndexOf(
            ApiTestFactory.ValidApiKey.First(char.IsLetter));
        var originalChar = ApiTestFactory.ValidApiKey[letterIndex];
        var flippedChar = char.IsUpper(originalChar) ? char.ToLowerInvariant(originalChar) : char.ToUpperInvariant(originalChar);
        var caseFlippedKey = ApiTestFactory.ValidApiKey[..letterIndex] + flippedChar + ApiTestFactory.ValidApiKey[(letterIndex + 1)..];
        Assert.Equal(ApiTestFactory.ValidApiKey.Length, caseFlippedKey.Length);
        Assert.NotEqual(ApiTestFactory.ValidApiKey, caseFlippedKey, StringComparer.Ordinal);
        Assert.Equal(ApiTestFactory.ValidApiKey, caseFlippedKey, StringComparer.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Add(ApiKeyHeader, caseFlippedKey);

        var response = await _client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithConfiguredApiKeyMissingOneCharacterInTheMiddle_ShouldReturn401()
    {
        var middleIndex = ApiTestFactory.ValidApiKey.Length / 2;
        var shortenedKey = ApiTestFactory.ValidApiKey.Remove(middleIndex, 1);
        Assert.Equal(ApiTestFactory.ValidApiKey.Length - 1, shortenedKey.Length);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
        request.Headers.Add(ApiKeyHeader, shortenedKey);

        var response = await _client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
