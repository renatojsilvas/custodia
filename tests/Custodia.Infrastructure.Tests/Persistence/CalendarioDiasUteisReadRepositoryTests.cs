using Custodia.Infrastructure.Persistence.Repositories;
using Dapper;
using Npgsql;

namespace Custodia.Infrastructure.Tests.Persistence;

[Collection("infra-postgres")]
public sealed class CalendarioDiasUteisReadRepositoryTests(InfrastructurePostgresFixture fixture)
{
    private static readonly int[] AnosDoHorizonteSemeado = [2024, 2025, 2026, 2027, 2028, 2029, 2030];

    private CalendarioDiasUteisReadRepository CriarRepositorio() =>
        new(NpgsqlDataSource.Create(fixture.ConnectionString));

    private static DateOnly CalcularPascoaPorAlgoritmoGregorianoIndependenteDaMigration(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(ano, mes, dia);
    }

    private static HashSet<DateOnly> ConstruirFeriadosEsperadosDoAno(int ano)
    {
        var pascoa = CalcularPascoaPorAlgoritmoGregorianoIndependenteDaMigration(ano);

        return
        [
            new DateOnly(ano, 1, 1),
            new DateOnly(ano, 4, 21),
            new DateOnly(ano, 5, 1),
            new DateOnly(ano, 9, 7),
            new DateOnly(ano, 10, 12),
            new DateOnly(ano, 11, 2),
            new DateOnly(ano, 11, 15),
            new DateOnly(ano, 11, 20),
            new DateOnly(ano, 12, 25),
            pascoa.AddDays(-48),
            pascoa.AddDays(-47),
            pascoa.AddDays(-2),
            pascoa.AddDays(60),
        ];
    }

    private async Task<HashSet<DateOnly>> ObterDatasSemeadasDoAnoAsync(int ano)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var datas = await connection.QueryAsync<DateTime>(
            "SELECT data FROM calendario_dias_uteis WHERE data >= @inicio AND data <= @fim",
            new { inicio = new DateTime(ano, 1, 1), fim = new DateTime(ano, 12, 31) });

        return datas.Select(DateOnly.FromDateTime).ToHashSet();
    }

    [Fact]
    public async Task ObterProximoDiaUtilAsync_DeUmaSextaFeira_DevolveASegundaFeiraSeguinte()
    {
        var repositorio = CriarRepositorio();

        var resultado = await repositorio.ObterProximoDiaUtilAsync(new DateOnly(2024, 1, 5), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Encontrado);
        Assert.Equal(new DateOnly(2024, 1, 8), resultado.Value.Valor);
    }

    [Fact]
    public async Task ObterProximoDiaUtilAsync_DeUmSabado_DevolveASegundaFeiraSeguinte()
    {
        var repositorio = CriarRepositorio();

        var resultado = await repositorio.ObterProximoDiaUtilAsync(new DateOnly(2024, 1, 6), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Encontrado);
        Assert.Equal(new DateOnly(2024, 1, 8), resultado.Value.Valor);
    }

    [Fact]
    public async Task ObterProximoDiaUtilAsync_DaVesperaDeNatal_PulaOFeriadoENaoApenasOFimDeSemana()
    {
        var repositorio = CriarRepositorio();

        var resultado = await repositorio.ObterProximoDiaUtilAsync(new DateOnly(2024, 12, 24), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Encontrado);
        Assert.Equal(new DateOnly(2024, 12, 26), resultado.Value.Valor);
    }

    [Fact]
    public async Task ObterProximoDiaUtilAsync_ComDataDentroDoHorizonte_DevolveEncontrado_ControlePositivoDoTesteDeHorizonteEsgotado()
    {
        var repositorio = CriarRepositorio();

        var resultado = await repositorio.ObterProximoDiaUtilAsync(new DateOnly(2030, 12, 30), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value.Encontrado);
        Assert.Equal(new DateOnly(2030, 12, 31), resultado.Value.Valor);
    }

    [Fact]
    public async Task ObterProximoDiaUtilAsync_NoUltimoDiaDoHorizonteSemeado_DevolveNaoEncontrado()
    {
        var repositorio = CriarRepositorio();

        var resultado = await repositorio.ObterProximoDiaUtilAsync(new DateOnly(2030, 12, 31), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value.Encontrado);
    }

    [Fact]
    public async Task ObterHorizonteAsync_DevolveODataMaximaSemeadaEHojeCalculadoNoMesmoRelogioDoTrigger()
    {
        var repositorio = CriarRepositorio();

        var resultado = await repositorio.ObterHorizonteAsync(CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(new DateOnly(2030, 12, 31), resultado.Value.DataMaxima);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var hojeEsperado = await connection.ExecuteScalarAsync<DateTime>(
            "SELECT (now() AT TIME ZONE 'America/Sao_Paulo')::date");

        Assert.Equal(DateOnly.FromDateTime(hojeEsperado), resultado.Value.Hoje);
    }

    [Fact]
    public async Task ObterHorizonteAsync_DataMinimaSemeadaEhOPrimeiroDiaUtilAPartirDeDoisDeJaneiroDeDoisMilEVinteEQuatro()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        var minimoSemeado = await connection.ExecuteScalarAsync<DateTime>(
            "SELECT MIN(data) FROM calendario_dias_uteis");

        Assert.Equal(new DateOnly(2024, 1, 2), DateOnly.FromDateTime(minimoSemeado));
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(2025)]
    [InlineData(2026)]
    [InlineData(2027)]
    [InlineData(2028)]
    [InlineData(2029)]
    [InlineData(2030)]
    public async Task CalendarioSemeado_NaoContagemMasConjuntoCompleto_ExcluiTodosOsFeriadosDoAnoEContemTodosOsDemaisDiasUteis(int ano)
    {
        var feriadosEsperados = ConstruirFeriadosEsperadosDoAno(ano);
        var datasSemeadas = await ObterDatasSemeadasDoAnoAsync(ano);

        foreach (var feriado in feriadosEsperados)
        {
            Assert.DoesNotContain(feriado, datasSemeadas);
        }

        for (var dia = new DateOnly(ano, 1, 1); dia <= new DateOnly(ano, 12, 31); dia = dia.AddDays(1))
        {
            var ehFimDeSemana = dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            if (ehFimDeSemana || feriadosEsperados.Contains(dia))
            {
                continue;
            }

            Assert.Contains(dia, datasSemeadas);
        }
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(2025)]
    [InlineData(2026)]
    [InlineData(2027)]
    [InlineData(2028)]
    [InlineData(2029)]
    [InlineData(2030)]
    public async Task CalendarioSemeado_QuartaFeiraDeCinzas_EhDiaUtilPorqueNaoEhFeriadoAnbimaEmNenhumAnoDoHorizonte(int ano)
    {
        var pascoa = CalcularPascoaPorAlgoritmoGregorianoIndependenteDaMigration(ano);
        var quartaDeCinzas = pascoa.AddDays(-46);
        var datasSemeadas = await ObterDatasSemeadasDoAnoAsync(ano);

        Assert.Contains(quartaDeCinzas, datasSemeadas);
    }
}
