using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeMovimentoTravamentoRepository(
    IReadOnlyList<Movimento> movimentosExistentes,
    Func<string, string, Result<MovimentoConsulta>>? travar = null)
    : IMovimentoTravamentoRepository
{
    private readonly Func<string, string, Result<MovimentoConsulta>> _travar =
        travar ?? DefaultTravar(movimentosExistentes);

    public List<(string ClienteId, string RefExterna)> Travamentos { get; } = [];

    public Task<Result<MovimentoConsulta>> TravarPorClienteERefExternaAsync(
        string clienteId, string refExterna, CancellationToken ct)
    {
        Travamentos.Add((clienteId, refExterna));
        return Task.FromResult(_travar(clienteId, refExterna));
    }

    private static Func<string, string, Result<MovimentoConsulta>> DefaultTravar(
        IReadOnlyList<Movimento> movimentos) =>
        (clienteId, refExterna) =>
        {
            var achado = movimentos.FirstOrDefault(m => m.ClienteId == clienteId && m.RefExterna == refExterna);

            return achado is null
                ? Result<MovimentoConsulta>.Success(MovimentoConsulta.NaoEncontrado)
                : Result<MovimentoConsulta>.Success(MovimentoConsulta.DeLinha(achado));
        };
}
