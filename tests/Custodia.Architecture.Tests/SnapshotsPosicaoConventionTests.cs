using System.Text.RegularExpressions;

namespace Custodia.Architecture.Tests;

public sealed partial class SnapshotsPosicaoConventionTests
{
    private static readonly string RepoRoot = RepoRootLocator.LocalizarRaizDoRepo();

    [Fact]
    public void NenhumCaminhoDeCodigoForaDePersistence_EscreveEmSnapshotsPosicao()
    {
        var arquivosSrc = ListarArquivosCs(Path.Combine(RepoRoot, "src"));

        Assert.True(
            arquivosSrc.Count > 0,
            $"esta asserção só é uma convenção real se houver ao menos um arquivo .cs em src para " +
            $"inspecionar; encontrados: {arquivosSrc.Count} em '{RepoRoot}'");

        var ocorrenciasForaDoPermitido = EncontrarOcorrencias(arquivosSrc)
            .Where(o => !EhCaminhoPermitido(o.Arquivo))
            .ToList();

        Assert.True(
            ocorrenciasForaDoPermitido.Count == 0,
            "nenhum caminho de código fora de Persistence/Configurations e Persistence/Migrations pode " +
            "mencionar 'snapshots_posicao' — não existe escritor de snapshot nesta fase (ADR-9: preço " +
            "individual nunca dispara a valoração diária; o handler de eod.ready nasce só no F7). " +
            "Ocorrências encontradas:\n" + string.Join('\n', ocorrenciasForaDoPermitido.Select(o => o.Descricao)));
    }

    [Fact]
    public void EncontrarOcorrencias_AchaAOcorrenciaRealEmSnapshotPosicaoConfiguration_ControlePositivo()
    {
        var arquivosSrc = ListarArquivosCs(Path.Combine(RepoRoot, "src"));

        var ocorrencias = EncontrarOcorrencias(arquivosSrc);

        Assert.Contains(
            ocorrencias,
            o => o.Arquivo.EndsWith("SnapshotPosicaoConfiguration.cs", StringComparison.Ordinal));

        Assert.True(
            ocorrencias.All(o => EhCaminhoPermitido(o.Arquivo)),
            "controle positivo: nesta fase a ÚNICA ocorrência esperada de 'snapshots_posicao' em src é a " +
            "configuração do EF em Persistence/Configurations — se este teste passar por vacuidade (zero " +
            "ocorrências no total), o teste principal também passaria por vacuidade.");
    }

    private static bool EhCaminhoPermitido(string arquivo) =>
        ContemSegmento(arquivo, "Configurations") || ContemSegmento(arquivo, "Migrations");

    private static bool ContemSegmento(string caminho, string segmentoEsperado) =>
        caminho.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segmento => segmento == segmentoEsperado);

    private static List<(string Arquivo, string Descricao)> EncontrarOcorrencias(IReadOnlyList<string> arquivos)
    {
        var ocorrencias = new List<(string Arquivo, string Descricao)>();

        foreach (var arquivo in arquivos)
        {
            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                if (PalavraSnapshotsPosicao().IsMatch(linhas[i]))
                {
                    ocorrencias.Add((arquivo, $"{arquivo}:{i + 1}: {linhas[i].Trim()}"));
                }
            }
        }

        return ocorrencias;
    }

    private static List<string> ListarArquivosCs(string diretorio)
    {
        if (!Directory.Exists(diretorio))
        {
            return [];
        }

        return Directory.EnumerateFiles(diretorio, "*.cs", SearchOption.AllDirectories)
            .Where(caminho => !ContemSegmentoBinOuObj(caminho))
            .ToList();
    }

    private static bool ContemSegmentoBinOuObj(string caminho)
    {
        var segmentos = caminho.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segmentos.Any(s => s is "bin" or "obj");
    }

    [GeneratedRegex(@"\bsnapshots_posicao\b")]
    private static partial Regex PalavraSnapshotsPosicao();
}
