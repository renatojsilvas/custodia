using Custodia.Domain.Eventos;

namespace Custodia.Domain.Tests.Eventos;

public sealed class MotivoParkingTests
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
        "falha_inesperada_no_processamento",
    ];

    [Fact]
    public void All_TemExatamenteCatorzeValores()
    {
        Assert.Equal(14, MotivoParking.All.Count);
    }

    [Fact]
    public void All_EhOConjuntoFechadoLiteralDestaFase_NemUmAMenosNemUmAMais()
    {
        var nomes = MotivoParking.All.Select(m => m.Name).ToHashSet();
        var esperado = ConjuntoFechadoEsperado.ToHashSet();

        Assert.Equal(esperado, nomes);
    }

    [Theory]
    [MemberData(nameof(NomesEsperados))]
    public void FromName_ResolveCadaValorDoConjuntoFechado(string nome)
    {
        var resultado = MotivoParking.FromName(nome);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(nome, resultado.Value.Name);
    }

    public static IEnumerable<object[]> NomesEsperados() =>
        ConjuntoFechadoEsperado.Select(nome => new object[] { nome });

    [Fact]
    public void FromName_NomeDesconhecido_DevolveFalha()
    {
        var resultado = MotivoParking.FromName("motivo_que_nao_existe");

        Assert.True(resultado.IsFailure);
        Assert.Equal(MotivoParkingErrors.MotivoInvalido, resultado.Error);
    }

    [Fact]
    public void FromName_NuloOuVazio_DevolveFalha()
    {
        Assert.True(MotivoParking.FromName(null).IsFailure);
        Assert.True(MotivoParking.FromName(string.Empty).IsFailure);
    }

    [Fact]
    public void ConjuntoFechadoEsperado_NaoContemOsDoisMotivosDeFasesFuturas()
    {
        Assert.DoesNotContain("intervalo_acima_do_teto", ConjuntoFechadoEsperado);
        Assert.DoesNotContain("acao_desconhecida", ConjuntoFechadoEsperado);
    }
}
