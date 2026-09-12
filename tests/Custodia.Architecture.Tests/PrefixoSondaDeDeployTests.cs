using System.Text.RegularExpressions;
using Custodia.Application.Eventos;

namespace Custodia.Architecture.Tests;

public sealed partial class PrefixoSondaDeDeployTests
{
    private static readonly string CaminhoDoScript = Path.Combine(
        RepoRootLocator.LocalizarRaizDoRepo(), "infra", "rabbitmq", "declare-topology.sh");

    [Fact]
    public void PrefixoSondaDeDeploy_ConcordaComOCustodiaProbePrefixoDoDeclareTopologySh()
    {
        var valorNoScript = ExtrairPrefixoDoConteudo(LerScript(CaminhoDoScript));

        Assert.Equal(RoteadorDeEventos.PrefixoSondaDeDeploy, valorNoScript);
    }

    [Fact]
    public void ExtrairPrefixoDoConteudo_AchaOValorRealDoScript_ControlePositivo()
    {
        var valorNoScript = ExtrairPrefixoDoConteudo(LerScript(CaminhoDoScript));

        Assert.Equal("custodia-f2-", valorNoScript);
    }

    [Fact]
    public void ExtrairPrefixoDoConteudo_DetectaDivergenciaEmConteudoSintetico_ControlePositivo()
    {
        const string conteudoSintetico = "CUSTODIA_PROBE_PREFIXO=\"custodia-f2-mudou-\"\n";

        var valorExtraidoDoSintetico = ExtrairPrefixoDoConteudo(conteudoSintetico);

        Assert.Equal("custodia-f2-mudou-", valorExtraidoDoSintetico);
        Assert.NotEqual(RoteadorDeEventos.PrefixoSondaDeDeploy, valorExtraidoDoSintetico);
    }

    [Fact]
    public void ExtrairPrefixoDoConteudo_FalhaDeFormaLegivelQuandoALinhaSumiuDoConteudo()
    {
        const string conteudoSemAAtribuicao = "echo 'declare-topology.sh sem a variavel de prova alguma'\n";

        var excecao = Record.Exception(() => ExtrairPrefixoDoConteudo(conteudoSemAAtribuicao));

        var invalidOperationException = Assert.IsType<InvalidOperationException>(excecao);
        Assert.Contains("CUSTODIA_PROBE_PREFIXO", invalidOperationException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LerScript_FalhaDeFormaLegivelQuandoOArquivoNaoExiste()
    {
        var caminhoInexistente = Path.Combine(
            Path.GetTempPath(), $"declare-topology-inexistente-{Guid.NewGuid():N}.sh");

        var excecao = Record.Exception(() => LerScript(caminhoInexistente));

        var invalidOperationException = Assert.IsType<InvalidOperationException>(excecao);
        Assert.Contains(caminhoInexistente, invalidOperationException.Message, StringComparison.Ordinal);
    }

    private static string LerScript(string caminho)
    {
        if (!File.Exists(caminho))
        {
            throw new InvalidOperationException(
                $"declare-topology.sh não encontrado em '{caminho}'. A origem do prefixo da sonda de " +
                "deploy (CUSTODIA_PROBE_PREFIXO) sumiu ou o script mudou de lugar — sem ele este teste " +
                "não tem contra o que confrontar a constante do C#, e não pode passar por vacuidade.");
        }

        return File.ReadAllText(caminho);
    }

    private static string ExtrairPrefixoDoConteudo(string conteudoDoScript)
    {
        var ocorrencias = PadraoDaAtribuicao().Matches(conteudoDoScript);

        if (ocorrencias.Count != 1)
        {
            throw new InvalidOperationException(
                "esperava exatamente uma atribuição de CUSTODIA_PROBE_PREFIXO=\"...\" no conteúdo " +
                $"analisado; encontrei {ocorrencias.Count}. Sem exatamente uma, não há valor único contra " +
                "o qual confrontar a constante do C#.");
        }

        return ocorrencias[0].Groups["valor"].Value;
    }

    [GeneratedRegex("""^CUSTODIA_PROBE_PREFIXO="(?<valor>[^"]*)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex PadraoDaAtribuicao();
}
