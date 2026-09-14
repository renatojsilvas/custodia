using System.Text.RegularExpressions;

namespace Custodia.Architecture.Tests;

public sealed partial class WriteRepositoriesDapperConventionTests
{
    private static readonly string RepoRoot = RepoRootLocator.LocalizarRaizDoRepo();

    private static readonly string DiretorioRepositories = Path.Combine(
        RepoRoot, "src", "Custodia.Infrastructure", "Persistence", "Repositories");

    [Fact]
    public void NenhumWriteRepository_UsaDapper()
    {
        var arquivos = ListarArquivosWriteRepository();

        Assert.True(
            arquivos.Count > 0,
            $"esta asserção só é uma convenção real se houver ao menos um *WriteRepository.cs em " +
            $"'{DiretorioRepositories}' para inspecionar; encontrados: {arquivos.Count}");

        var ocorrencias = EncontrarOcorrenciasDeDapper(arquivos);

        Assert.True(
            ocorrencias.Count == 0,
            "nenhum *WriteRepository.cs deve usar Dapper (deve ler dentro da transação ambiente via EF " +
            "— FromSqlInterpolated/SqlQuery —, como MovimentoTravamentoRepository, e não via Dapper sobre " +
            "a conexão obtida de dbContext.Database.GetDbConnection() — terceira técnica de acesso a " +
            "dados na mesma classe). Ocorrências encontradas:\n" + string.Join('\n', ocorrencias));
    }

    [Fact]
    public void PalavraDapper_DetectaOcorrenciaRealEIgnoraTextoSemAPalavra()
    {
        Assert.Matches(PalavraDapper(), "using Dapper;");
        Assert.Matches(PalavraDapper(), "var conexao = (NpgsqlConnection)dbContext.Database.GetDbConnection();\nDapper.SqlMapper.Query();");
        Assert.DoesNotMatch(PalavraDapper(), "public sealed class Foo");
        Assert.DoesNotMatch(PalavraDapper(), "var dapperzinho = 1;");

        var caminhoTemporario = EscreverArquivoTemporarioComDapper();
        try
        {
            Assert.True(
                EncontrarOcorrenciasDeDapper([caminhoTemporario]).Count == 1,
                "esta asserção prova, contra um arquivo real em disco, que o scanner usado pelo teste " +
                "'NenhumWriteRepository_UsaDapper' enxerga uma ocorrência de verdade quando ela existe — " +
                "o controle positivo exigido pelo PADROES §10.8 para a asserção negativa acima. Nenhum " +
                "*WriteRepository.cs real usa Dapper hoje, então o controle não pode vir de código de " +
                "produção.");
        }
        finally
        {
            File.Delete(caminhoTemporario);
        }
    }

    private static string EscreverArquivoTemporarioComDapper()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"controle-positivo-dapper-{Guid.NewGuid():N}.cs");
        File.WriteAllText(caminho, "using Dapper;\n\nvar x = connection.QuerySingleAsync<int>(\"SELECT 1\");");
        return caminho;
    }

    private static List<string> ListarArquivosWriteRepository()
    {
        if (!Directory.Exists(DiretorioRepositories))
        {
            return [];
        }

        return Directory.EnumerateFiles(DiretorioRepositories, "*WriteRepository.cs", SearchOption.AllDirectories)
            .Where(caminho => !ContemSegmentoBinOuObj(caminho))
            .ToList();
    }

    private static List<string> EncontrarOcorrenciasDeDapper(IReadOnlyList<string> arquivos)
    {
        var ocorrencias = new List<string>();

        foreach (var arquivo in arquivos)
        {
            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                if (PalavraDapper().IsMatch(linhas[i]))
                {
                    ocorrencias.Add($"{arquivo}:{i + 1}: {linhas[i].Trim()}");
                }
            }
        }

        return ocorrencias;
    }

    private static bool ContemSegmentoBinOuObj(string caminho)
    {
        var segmentos = caminho.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segmentos.Any(s => s is "bin" or "obj");
    }

    [GeneratedRegex(@"\bDapper\b")]
    private static partial Regex PalavraDapper();
}
