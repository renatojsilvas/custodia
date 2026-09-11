using Custodia.Domain.Movimentos;

namespace Custodia.Application.Movimentos;

public sealed record MovimentoConsulta(bool Encontrado, Movimento? Linha)
{
    public static readonly MovimentoConsulta NaoEncontrado = new(false, null);

    public static MovimentoConsulta DeLinha(Movimento linha) => new(true, linha);
}
