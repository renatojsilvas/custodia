namespace Custodia.Architecture.Tests;

internal static class RepoRootLocator
{
    public static string LocalizarRaizDoRepo()
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
}
