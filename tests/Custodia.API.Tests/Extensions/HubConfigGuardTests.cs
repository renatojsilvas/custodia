using Custodia.API.Extensions;

namespace Custodia.API.Tests.Extensions;

public sealed class HubConfigGuardTests
{
    private const string ValidBaseUrl = "http://hub-precos-app:8080/";
    private const string ValidApiKey = "hub-api-key-com-mais-de-trinta-e-dois-caracteres";

    [Fact]
    public void Validate_Production_ComBaseUrlVazia_LancaComNomeDaChaveNaMensagem()
    {
        var act = () => HubConfigGuard.Validate("Production", "", ValidApiKey);

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Hub:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_ComBaseUrlNula_Lanca()
    {
        var act = () => HubConfigGuard.Validate("Production", null, ValidApiKey);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Validate_Production_ComBaseUrlRelativa_Lanca()
    {
        var act = () => HubConfigGuard.Validate("Production", "/v1/prices/asof", ValidApiKey);

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Hub:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_ComEsquemaNaoHttp_Lanca()
    {
        var act = () => HubConfigGuard.Validate("Production", "ftp://hub-precos-app/", ValidApiKey);

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Hub:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_ComBaseUrlHttpsValida_NaoLanca()
    {
        HubConfigGuard.Validate("Production", "https://hub.example.com/", ValidApiKey);
    }

    [Fact]
    public void Validate_Production_ComApiKeyVazia_LancaComNomeDaChaveNaMensagem()
    {
        var act = () => HubConfigGuard.Validate("Production", ValidBaseUrl, "");

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Hub:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_ComApiKeyNula_Lanca()
    {
        var act = () => HubConfigGuard.Validate("Production", ValidBaseUrl, null);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Validate_Production_ComApiKeyEmBranco_Lanca()
    {
        var act = () => HubConfigGuard.Validate("Production", ValidBaseUrl, "   ");

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Validate_Production_ComBaseUrlEApiKeyValidas_NaoLanca()
    {
        HubConfigGuard.Validate("Production", ValidBaseUrl, ValidApiKey);
    }

    [Fact]
    public void Validate_Development_ComBaseUrlEApiKeyVazias_NaoLanca()
    {
        HubConfigGuard.Validate("Development", "", "");
    }

    [Fact]
    public void Validate_Testing_ComBaseUrlEApiKeyVazias_NaoLanca()
    {
        HubConfigGuard.Validate("Testing", "", "");
    }

    [Fact]
    public void Validate_AmbienteDesconhecido_ComBaseUrlEApiKeyVazias_Lanca()
    {
        var act = () => HubConfigGuard.Validate("Staging", "", "");

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Validate_Production_ComApiKeyAbaixoDoComprimentoMinimo_LancaComNomeDaChaveNaMensagem()
    {
        var act = () => HubConfigGuard.Validate("Production", ValidBaseUrl, new string('a', 31));

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("Hub:ApiKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("abaixo do mínimo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_ComApiKeyNoComprimentoMinimo_NaoLanca()
    {
        HubConfigGuard.Validate("Production", ValidBaseUrl, new string('a', 32));
    }

    [Theory]
    [InlineData("CHANGE-ME-IN-PRODUCTION")]
    [InlineData("dev-local-key")]
    [InlineData("uma-chave-qualquer-para-dev")]
    public void Validate_Production_ComApiKeyPlaceholderPreenchidaAcimaDoMinimo_LancaPorPlaceholder(
        string placeholderBloqueado)
    {
        var preenchida = placeholderBloqueado + new string('x', 40 - placeholderBloqueado.Length);
        Assert.True(preenchida.Length >= 32, "O preenchimento precisa deixar a chave com 32+ caracteres.");

        var act = () => HubConfigGuard.Validate("Production", ValidBaseUrl, preenchida);

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("placeholder conhecido", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abaixo do mínimo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_ComApiKeyPlaceholderComSeparadorDiferente_Lanca()
    {
        var act = () => HubConfigGuard.Validate(
            "Production", ValidBaseUrl, "change_me_in_production_padded_xxxx");

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("placeholder conhecido", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_AmbienteIsento_ComApiKeyPlaceholder_NaoLanca(string environmentName)
    {
        HubConfigGuard.Validate(environmentName, "", "CHANGE-ME-IN-PRODUCTION");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_AmbienteIsento_ComApiKeyCurta_NaoLanca(string environmentName)
    {
        HubConfigGuard.Validate(environmentName, "", "123");
    }
}
