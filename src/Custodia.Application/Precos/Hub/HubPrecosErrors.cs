using Custodia.Domain.Common;

namespace Custodia.Application.Precos.Hub;

public static class HubPrecosErrors
{
    public static readonly Error HubIndisponivel = new(
        "Hub.Indisponivel",
        "Não foi possível falar com o Hub de Preços agora (transporte, 5xx, timeout ou circuito aberto); tente novamente.",
        ErrorType.Unavailable);

    public static readonly Error HubColetaIncompleta = new(
        "Hub.ColetaIncompleta",
        "A fatia de preços do Hub não coube na coleta ou não passou na conferência de completude; " +
        "resultado seria parcial e não foi devolvido.",
        ErrorType.Unavailable);

    public static readonly Error HubAcessoNegado = new(
        "Hub.AcessoNegado",
        "O Hub recusou a chamada por autenticação/autorização (401/403); é configuração — repetir não conserta.",
        ErrorType.Unavailable);

    public static readonly Error HubRespostaInvalida = new(
        "Hub.RespostaInvalida",
        "O Hub devolveu uma resposta fora do contrato esperado (4xx, JSON ilegível, campo obrigatório ausente, " +
        "valor/data ilegível ou motivo desconhecido).",
        ErrorType.Unavailable);
}
