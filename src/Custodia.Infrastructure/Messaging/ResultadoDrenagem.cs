namespace Custodia.Infrastructure.Messaging;

public enum DesfechoDrenagem
{
    Completude,
    Parcial,
    VazioDoMotivo,
    LimitePorTeto,
    LimitePorVolta,
    Interrompida,
}

public sealed record ResultadoDrenagem(
    DesfechoDrenagem Desfecho,
    string Motivo,
    string PassagemId,
    long Estoque,
    long Teto,
    long NMotivo,
    long ResidualMotivo);
