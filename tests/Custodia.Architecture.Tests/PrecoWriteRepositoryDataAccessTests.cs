using System.Text.RegularExpressions;

namespace Custodia.Architecture.Tests;

public sealed partial class PrecoWriteRepositoryDataAccessTests
{
    private static readonly string RepoRoot = RepoRootLocator.LocalizarRaizDoRepo();

    private static readonly string CaminhoDoArquivo = Path.Combine(
        RepoRoot, "src", "Custodia.Infrastructure", "Persistence", "Repositories", "PrecoWriteRepository.cs");

    [Fact]
    public void PrecoWriteRepository_NaoUsaDapper()
    {
        Assert.True(File.Exists(CaminhoDoArquivo), $"arquivo não encontrado: {CaminhoDoArquivo}");

        var conteudo = File.ReadAllText(CaminhoDoArquivo);

        Assert.False(
            PalavraDapper().IsMatch(conteudo),
            "PrecoWriteRepository deve ler dentro da transação ambiente via EF " +
            "(FromSqlInterpolated/SqlQuery), como MovimentoTravamentoRepository, e não via Dapper " +
            "sobre a conexão obtida de dbContext.Database.GetDbConnection() — terceira técnica de " +
            "acesso a dados na mesma classe.");
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
            var conteudo = File.ReadAllText(caminhoTemporario);
            Assert.True(
                PalavraDapper().IsMatch(conteudo),
                "esta asserção prova, contra um arquivo real em disco, que o scanner usado pelo teste " +
                "'PrecoWriteRepository_NaoUsaDapper' enxerga uma ocorrência de verdade quando ela existe " +
                "— o controle positivo exigido pelo PADROES §10.8 para a asserção negativa acima.");
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

    [GeneratedRegex(@"\bDapper\b")]
    private static partial Regex PalavraDapper();
}
