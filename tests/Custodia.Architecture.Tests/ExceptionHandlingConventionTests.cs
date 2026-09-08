using System.Text.RegularExpressions;

namespace Custodia.Architecture.Tests;

public sealed partial class ExceptionHandlingConventionTests
{
    private static readonly string RepoRoot = LocalizarRaizDoRepo();

    [Fact]
    public void DomainEApplication_NaoDevemConterCatch()
    {
        var arquivosDomain = ListarArquivosCs(Path.Combine(RepoRoot, "src", "Custodia.Domain"));
        var arquivosApplication = ListarArquivosCs(Path.Combine(RepoRoot, "src", "Custodia.Application"));
        var arquivosInfrastructure = ListarArquivosCs(Path.Combine(RepoRoot, "src", "Custodia.Infrastructure"));

        Assert.True(
            arquivosDomain.Count > 0,
            $"esta asserção só é uma convenção real se houver ao menos um arquivo .cs " +
            $"em Custodia.Domain para inspecionar; encontrados: {arquivosDomain.Count} em '{RepoRoot}'");
        Assert.True(
            arquivosApplication.Count > 0,
            $"esta asserção só é uma convenção real se houver ao menos um arquivo .cs " +
            $"em Custodia.Application para inspecionar; encontrados: {arquivosApplication.Count} em '{RepoRoot}'");
        Assert.True(
            arquivosInfrastructure.Count > 0,
            $"esta asserção só é uma convenção real se houver ao menos um arquivo .cs " +
            $"em Custodia.Infrastructure para inspecionar; encontrados: {arquivosInfrastructure.Count} em '{RepoRoot}'");

        var catchesDomain = EncontrarOcorrenciasDeCatch(arquivosDomain);
        var catchesApplication = EncontrarOcorrenciasDeCatch(arquivosApplication);

        Assert.True(
            catchesDomain.Count == 0,
            "Custodia.Domain não deve conter 'catch' (tratamento de exceção pertence à Infrastructure, " +
            "onde a exceção nasce). Ocorrências encontradas:\n" + string.Join('\n', catchesDomain));

        Assert.True(
            catchesApplication.Count == 0,
            "Custodia.Application não deve conter 'catch' (tratamento de exceção pertence à " +
            "Infrastructure, onde a exceção nasce). Ocorrências encontradas:\n" +
            string.Join('\n', catchesApplication));
    }

    [Fact]
    public void PalavraCatch_DetectaOcorrenciaRealEIgnoraTextoSemAPalavra()
    {
        Assert.Matches(PalavraCatch(), "catch (Exception ex)");
        Assert.Matches(PalavraCatch(), "        catch");
        Assert.DoesNotMatch(PalavraCatch(), "public sealed class Foo");
        Assert.DoesNotMatch(PalavraCatch(), "var catcher = 1;");

        Assert.True(
            EncontrarOcorrenciasDeCatch([EscreverArquivoTemporarioComCatch()]).Count == 1,
            "esta asserção prova, contra um arquivo real em disco, que o scanner usado pelo teste " +
            "'DomainEApplication_NaoDevemConterCatch' enxerga uma ocorrência de verdade quando ela " +
            "existe — o controle positivo exigido pelo PADROES §10.8 para a asserção negativa acima. " +
            "Custodia.Infrastructure não tem 'catch' nesta fase (não há tradução de exceção de banco " +
            "porque não há tabela nenhuma no modelo), então o controle não pode vir de código de produção.");
    }

    private static string EscreverArquivoTemporarioComCatch()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"controle-positivo-catch-{Guid.NewGuid():N}.cs");
        File.WriteAllText(caminho, "try { } catch (Exception ex) { throw; }");
        return caminho;
    }

    private static List<string> EncontrarOcorrenciasDeCatch(IReadOnlyList<string> arquivos)
    {
        var ocorrencias = new List<string>();

        foreach (var arquivo in arquivos)
        {
            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                if (PalavraCatch().IsMatch(linhas[i]))
                {
                    ocorrencias.Add($"{arquivo}:{i + 1}: {linhas[i].Trim()}");
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

    private static string LocalizarRaizDoRepo()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Custodia.sln")))
        {
            diretorio = diretorio.Parent;
        }

        if (diretorio is null)
        {
            throw new InvalidOperationException(
                $"Não foi possível localizar a raiz do repo (Custodia.sln) subindo a partir de " +
                $"'{AppContext.BaseDirectory}'.");
        }

        return diretorio.FullName;
    }

    [GeneratedRegex(@"\bcatch\b")]
    private static partial Regex PalavraCatch();
}
