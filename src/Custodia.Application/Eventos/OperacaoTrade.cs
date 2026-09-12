namespace Custodia.Application.Eventos;

public enum OperacaoTrade
{
    Aplicacao,
    Resgate,
    Aporte,
    Estorno,
}

public static class OperacaoTradeVocabulario
{
    private const string NomeAplicacao = "aplicacao";
    private const string NomeResgate = "resgate";
    private const string NomeAporte = "aporte";
    private const string NomeEstorno = "estorno";

    public static bool TryFromName(string? nome, out OperacaoTrade operacao)
    {
        switch (nome)
        {
            case NomeAplicacao:
                operacao = OperacaoTrade.Aplicacao;
                return true;
            case NomeResgate:
                operacao = OperacaoTrade.Resgate;
                return true;
            case NomeAporte:
                operacao = OperacaoTrade.Aporte;
                return true;
            case NomeEstorno:
                operacao = OperacaoTrade.Estorno;
                return true;
            default:
                operacao = default;
                return false;
        }
    }
}
