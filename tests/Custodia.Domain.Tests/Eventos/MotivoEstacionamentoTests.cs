using Custodia.Domain.Eventos;

namespace Custodia.Domain.Tests.Eventos;

public sealed class MotivoEstacionamentoTests
{
    private static readonly string[] ConjuntoFechadoEsperado =
    [
        "tipo_nao_tratado_prices",
        "tipo_nao_tratado_corpactions",
        "tipo_nao_tratado_eod",
        "estorno_orfao_expirado",
        "estorno_cliente_divergente",
        "estorno_duplicado",
        "estorno_divergente",
        "retry_indisponivel",
        "versao_nao_suportada",
        "payload_invalido",
        "origem_recurso_ausente",
        "origem_recurso_invalida",
        "identificador_com_espaco_na_borda",
    ];

    [Fact]
    public void All_TemExatamenteTrezeValores()
    {
        Assert.Equal(13, MotivoEstacionamento.All.Count);
    }

    [Fact]
    public void All_EhOConjuntoFechadoLiteralDestaFase_NemUmAMenosNemUmAMais()
    {
        var nomes = MotivoEstacionamento.All.Select(m => m.Name).ToHashSet();
        var esperado = ConjuntoFechadoEsperado.ToHashSet();

        Assert.Equal(esperado, nomes);
    }

    [Theory]
    [MemberData(nameof(NomesEsperados))]
    public void FromName_ResolveCadaValorDoConjuntoFechado(string nome)
    {
        var resultado = MotivoEstacionamento.FromName(nome);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(nome, resultado.Value.Name);
    }

    public static IEnumerable<object[]> NomesEsperados() =>
        ConjuntoFechadoEsperado.Select(nome => new object[] { nome });

    [Fact]
    public void FromName_NomeDesconhecido_DevolveFalha()
    {
        var resultado = MotivoEstacionamento.FromName("motivo_que_nao_existe");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MotivoEstacionamentoErrors.MotivoInvalido, resultado.Error);
    }

    [Fact]
    public void FromName_NuloOuVazio_DevolveFalha()
    {
        Assert.True(MotivoEstacionamento.FromName(null).IsFailure);
        Assert.True(MotivoEstacionamento.FromName(string.Empty).IsFailure);
    }

    [Fact]
    public void ConjuntoFechadoEsperado_NaoContemOsDoisMotivosDeFasesFuturas()
    {
        Assert.DoesNotContain("intervalo_acima_do_teto", ConjuntoFechadoEsperado);
        Assert.DoesNotContain("acao_desconhecida", ConjuntoFechadoEsperado);
    }
}
