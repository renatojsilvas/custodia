using Custodia.Domain.Movimentos;

namespace Custodia.Domain.Posicoes;

public readonly record struct MomentoDoLivro(DateOnly DataEvento, DateTimeOffset RegistradoEm)
{
    public static readonly MomentoDoLivro Infinito = new(DateOnly.MaxValue, DateTimeOffset.MaxValue);

    public bool Inclui(Movimento movimento) =>
        movimento.DataEvento < DataEvento
        || (movimento.DataEvento == DataEvento && movimento.RegistradoEm <= RegistradoEm);
}
