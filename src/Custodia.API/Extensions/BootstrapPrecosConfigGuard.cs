using Custodia.Application.Precos.Bootstrap;
using Microsoft.Extensions.Configuration;

namespace Custodia.API.Extensions;

public static class BootstrapPrecosConfigGuard
{
    private const string TamanhoFatiaKey = "Decisao:BootstrapPrecosTamanhoFatia";
    private const int TamanhoMinimo = 1;
    private const int TamanhoMaximo = 200;

    public static void Validate(IConfiguration configuration)
    {
        var tamanhoFatia = configuration.GetValue<int?>(TamanhoFatiaKey)
            ?? ColetarPrecosDoHubCommandHandler.TamanhoFatiaPadrao;

        if (tamanhoFatia < TamanhoMinimo || tamanhoFatia > TamanhoMaximo)
        {
            throw new InvalidOperationException(
                $"Configuração inválida: '{TamanhoFatiaKey}' ({tamanhoFatia}) precisa estar entre {TamanhoMinimo} " +
                $"e {TamanhoMaximo}. O limite real é o tamanho da linha de requisição ao Hub " +
                "(GET .../v1/prices/asof?instruments=...): 500 ids 'td:' passam de 8 KB e voltam 414. Configure " +
                $"via variável de ambiente Decisao__BootstrapPrecosTamanhoFatia.");
        }
    }
}
