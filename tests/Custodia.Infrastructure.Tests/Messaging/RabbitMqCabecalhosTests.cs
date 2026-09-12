using Custodia.Infrastructure.Messaging;

namespace Custodia.Infrastructure.Tests.Messaging;

public sealed class RabbitMqCabecalhosTests
{
    [Fact]
    public void Copiar_OrigemNula_DevolveDicionarioVazio()
    {
        var copia = RabbitMqCabecalhos.Copiar(null);

        Assert.Empty(copia);
    }

    [Fact]
    public void Copiar_ClonaValoresEDescartaNulos()
    {
        var origem = new Dictionary<string, object?>
        {
            ["a"] = "1",
            ["b"] = null,
            ["c"] = 42L,
        };

        var copia = RabbitMqCabecalhos.Copiar(origem);

        Assert.Equal(2, copia.Count);
        Assert.Equal("1", copia["a"]);
        Assert.Equal(42L, copia["c"]);
        Assert.False(copia.ContainsKey("b"));

        copia["a"] = "mudou";
        Assert.Equal("1", origem["a"]);
    }

    [Fact]
    public void DefinirMotivo_EscreveOCabecalhoXCustodiaMotivo()
    {
        var cabecalhos = new Dictionary<string, object?>();

        RabbitMqCabecalhos.DefinirMotivo(cabecalhos, "estorno_orfao_expirado");

        Assert.Equal("estorno_orfao_expirado", cabecalhos["x-custodia-motivo"]);
    }

    [Fact]
    public void LerVoltas_SemCabecalho_DevolveZero()
    {
        var voltas = RabbitMqCabecalhos.LerVoltas(new Dictionary<string, object?>());

        Assert.Equal(0, voltas);
    }

    [Fact]
    public void LerVoltas_ComCabecalhoPresente_DevolveOValor()
    {
        var cabecalhos = new Dictionary<string, object?> { [RabbitMqCabecalhos.Voltas] = 5L };

        var voltas = RabbitMqCabecalhos.LerVoltas(cabecalhos);

        Assert.Equal(5, voltas);
    }

    [Fact]
    public void IncrementarVoltas_SemCabecalhoPrevio_EscreveUm()
    {
        var cabecalhos = new Dictionary<string, object?>();

        var voltas = RabbitMqCabecalhos.IncrementarVoltas(cabecalhos);

        Assert.Equal(1, voltas);
        Assert.Equal(1L, cabecalhos[RabbitMqCabecalhos.Voltas]);
    }

    [Fact]
    public void IncrementarVoltas_ChamadoDuasVezesSobreOMesmoDicionario_AcumulaAoInvesDeReiniciar()
    {
        var cabecalhos = new Dictionary<string, object?>();

        RabbitMqCabecalhos.IncrementarVoltas(cabecalhos);
        var segundaVolta = RabbitMqCabecalhos.IncrementarVoltas(cabecalhos);

        Assert.Equal(2, segundaVolta);
    }

    [Fact]
    public void IncrementarVoltas_ComCabecalhoCopiadoDeUmaMensagemAnterior_ContinuaDaContagemRecebida()
    {
        var cabecalhosRecebidos = new Dictionary<string, object?> { [RabbitMqCabecalhos.Voltas] = 7L };
        var cabecalhosParaRepublicar = RabbitMqCabecalhos.Copiar(cabecalhosRecebidos);

        var voltas = RabbitMqCabecalhos.IncrementarVoltas(cabecalhosParaRepublicar);

        Assert.Equal(8, voltas);
    }

    [Fact]
    public void ContarTentativasDeEntrega_SemCabecalho_DevolveZero()
    {
        var tentativas = RabbitMqCabecalhos.ContarTentativasDeEntrega(new Dictionary<string, object?>());

        Assert.Equal(0, tentativas);
    }

    [Theory]
    [InlineData((byte)2, 2L)]
    [InlineData((short)7, 7L)]
    [InlineData(9, 9L)]
    [InlineData(11L, 11L)]
    public void ContarTentativasDeEntrega_AceitaVariosTiposIntegraisDoClienteAmqp(object valorBruto, long esperado)
    {
        var cabecalhos = new Dictionary<string, object?> { ["x-acquired-count"] = valorBruto };

        var tentativas = RabbitMqCabecalhos.ContarTentativasDeEntrega(cabecalhos);

        Assert.Equal(esperado, tentativas);
    }

    [Fact]
    public void DefinirPassagemId_EscreveOCabecalhoXCustodiaPassagemId()
    {
        var cabecalhos = new Dictionary<string, object?>();

        RabbitMqCabecalhos.DefinirPassagemId(cabecalhos, "passagem-123");

        Assert.Equal("passagem-123", cabecalhos["x-custodia-passagem-id"]);
    }

    [Fact]
    public void LerPassagemId_SemCabecalho_DevolveNulo()
    {
        Assert.Null(RabbitMqCabecalhos.LerPassagemId(new Dictionary<string, object?>()));
    }

    [Fact]
    public void LerPassagemId_ComValorComoString_DevolveOValor()
    {
        var cabecalhos = new Dictionary<string, object?> { [RabbitMqCabecalhos.PassagemId] = "passagem-abc" };

        Assert.Equal("passagem-abc", RabbitMqCabecalhos.LerPassagemId(cabecalhos));
    }

    [Fact]
    public void LerPassagemId_ComValorComoBytesUtf8_DecodificaComoTexto()
    {
        var cabecalhos = new Dictionary<string, object?>
        {
            [RabbitMqCabecalhos.PassagemId] = System.Text.Encoding.UTF8.GetBytes("passagem-bytes"),
        };

        Assert.Equal("passagem-bytes", RabbitMqCabecalhos.LerPassagemId(cabecalhos));
    }

    [Fact]
    public void LerMotivo_ComValorComoBytesUtf8_DecodificaComoTexto()
    {
        var cabecalhos = new Dictionary<string, object?>
        {
            [RabbitMqCabecalhos.Motivo] = System.Text.Encoding.UTF8.GetBytes("payload_invalido"),
        };

        Assert.Equal("payload_invalido", RabbitMqCabecalhos.LerMotivo(cabecalhos));
    }

    [Fact]
    public void LerMotivo_CabecalhosNulos_DevolveNulo()
    {
        Assert.Null(RabbitMqCabecalhos.LerMotivo(null));
    }
}
