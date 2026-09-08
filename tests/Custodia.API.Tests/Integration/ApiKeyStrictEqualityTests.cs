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

    public static IEnumerable<object[]> EveryIndexOfConfiguredApiKey() =>
        Enumerable.Range(0, ApiTestFactory.ValidApiKey.Length).Select(index => new object[] { index });

    [Fact]
    public void EveryIndexOfConfiguredApiKey_ShouldNotBeEmpty()
    {
        var indexCount = EveryIndexOfConfiguredApiKey().Count();

        Assert.True(
            indexCount > 0,
            "O gerador de índices tem que devolver pelo menos um caso, senão o Theory " +
            "abaixo roda zero vezes e a classe inteira de mutação (§10.8) fica sem cobertura " +
            "sem que nenhum teste acuse. Se isto falhar, ApiTestFactory.ValidApiKey virou string vazia.");
    }

    [Theory]
    [MemberData(nameof(EveryIndexOfConfiguredApiKey))]
    public async Task Get_WithOneCharacterSwappedAtEachIndexOfConfiguredApiKey_ShouldReturn401(int indexToSwap)
    {
        var originalChar = ApiTestFactory.ValidApiKey[indexToSwap];
        var swappedChar = originalChar == 'z' ? 'y' : 'z';
        var swappedKey = ApiTestFactory.ValidApiKey[..indexToSwap] + swappedChar + ApiTestFactory.ValidApiKey[(indexToSwap + 1)..];
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
