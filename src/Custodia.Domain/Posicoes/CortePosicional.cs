using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Posicoes;

public readonly record struct CortePosicional(DateOnly DataEvento, DateTimeOffset RegistradoEm)
{
    public static readonly CortePosicional Infinito = new(DateOnly.MaxValue, DateTimeOffset.MaxValue);

    public bool Inclui(Movimento movimento) =>
        movimento.DataEvento < DataEvento
        || (movimento.DataEvento == DataEvento && movimento.RegistradoEm <= RegistradoEm);
}
