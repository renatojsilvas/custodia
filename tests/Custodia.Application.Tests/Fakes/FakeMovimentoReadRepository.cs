using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;

namespace Custodia.Application.Tests.Fakes;

internal sealed class FakeMovimentoReadRepository(
    IReadOnlyList<Movimento> movimentosExistentes,
    Func<string, string, Result<MovimentoConsulta>>? porClienteERefExterna = null,
    Func<string, Result<MovimentoConsulta>>? porRefExternaEmQualquerCliente = null,
    Func<string, string, Result<MaxDataEventoConsulta>>? maxDataEvento = null,
    Func<string, string, Result<IReadOnlyList<Movimento>>>? movimentosDaChave = null,
    Func<string?, string?, Result<IReadOnlyList<ChavePosicao>>>? chavesDistintas = null)
    : IMovimentoReadRepository
{
    private readonly Func<string, string, Result<MovimentoConsulta>> _porClienteERefExterna =
        porClienteERefExterna ?? DefaultPorClienteERefExterna(movimentosExistentes);

    private readonly Func<string, Result<MovimentoConsulta>> _porRefExternaEmQualquerCliente =
        porRefExternaEmQualquerCliente ?? DefaultPorRefExternaEmQualquerCliente(movimentosExistentes);

    private readonly Func<string, string, Result<MaxDataEventoConsulta>> _maxDataEvento =
        maxDataEvento ?? DefaultMaxDataEvento(movimentosExistentes);

    private readonly Func<string, string, Result<IReadOnlyList<Movimento>>> _movimentosDaChave =
        movimentosDaChave ?? DefaultMovimentosDaChave(movimentosExistentes);

    private readonly Func<string?, string?, Result<IReadOnlyList<ChavePosicao>>> _chavesDistintas =
        chavesDistintas ?? DefaultChavesDistintas(movimentosExistentes);

    public Task<Result<MovimentoConsulta>> ObterPorClienteERefExternaAsync(
        string clienteId, string refExterna, CancellationToken ct) =>
        Task.FromResult(_porClienteERefExterna(clienteId, refExterna));

    public Task<Result<MovimentoConsulta>> ObterPorRefExternaEmQualquerClienteAsync(
        string refExterna, CancellationToken ct) =>
        Task.FromResult(_porRefExternaEmQualquerCliente(refExterna));

    public Task<Result<MaxDataEventoConsulta>> ObterMaxDataEventoAsync(
        string clienteId, string instrumentoId, CancellationToken ct) =>
        Task.FromResult(_maxDataEvento(clienteId, instrumentoId));

    public Task<Result<IReadOnlyList<Movimento>>> ObterMovimentosDaChaveAsync(
        string clienteId, string instrumentoId, CancellationToken ct) =>
        Task.FromResult(_movimentosDaChave(clienteId, instrumentoId));

    public Task<Result<IReadOnlyList<ChavePosicao>>> ObterChavesDistintasAsync(
        string? clienteId, string? instrumentoId, CancellationToken ct) =>
        Task.FromResult(_chavesDistintas(clienteId, instrumentoId));

    private static Func<string, string, Result<MovimentoConsulta>> DefaultPorClienteERefExterna(
        IReadOnlyList<Movimento> movimentos) =>
        (clienteId, refExterna) =>
        {
            var achado = movimentos.FirstOrDefault(
                m => m.ClienteId == clienteId && m.RefExterna == refExterna);

            return achado is null
                ? Result<MovimentoConsulta>.Success(MovimentoConsulta.NaoEncontrado)
                : Result<MovimentoConsulta>.Success(MovimentoConsulta.DeLinha(achado));
        };

    private static Func<string, Result<MovimentoConsulta>> DefaultPorRefExternaEmQualquerCliente(
        IReadOnlyList<Movimento> movimentos) =>
        refExterna =>
        {
            var achado = movimentos.FirstOrDefault(m => m.RefExterna == refExterna);
            return achado is null
                ? Result<MovimentoConsulta>.Success(MovimentoConsulta.NaoEncontrado)
                : Result<MovimentoConsulta>.Success(MovimentoConsulta.DeLinha(achado));
        };

    private static Func<string, string, Result<MaxDataEventoConsulta>> DefaultMaxDataEvento(
        IReadOnlyList<Movimento> movimentos) =>
        (clienteId, instrumentoId) =>
        {
            var daChave = movimentos
                .Where(m => m.ClienteId == clienteId && m.InstrumentoId == instrumentoId)
                .ToList();

            return daChave.Count == 0
                ? Result<MaxDataEventoConsulta>.Success(MaxDataEventoConsulta.Inexistente)
                : Result<MaxDataEventoConsulta>.Success(MaxDataEventoConsulta.De(daChave.Max(m => m.DataEvento)));
        };

    private static Func<string, string, Result<IReadOnlyList<Movimento>>> DefaultMovimentosDaChave(
        IReadOnlyList<Movimento> movimentos) =>
        (clienteId, instrumentoId) =>
        {
            IReadOnlyList<Movimento> daChave = movimentos
                .Where(m => m.ClienteId == clienteId && m.InstrumentoId == instrumentoId)
                .ToList();

            return Result<IReadOnlyList<Movimento>>.Success(daChave);
        };

    private static Func<string?, string?, Result<IReadOnlyList<ChavePosicao>>> DefaultChavesDistintas(
        IReadOnlyList<Movimento> movimentos) =>
        (clienteId, instrumentoId) =>
        {
            IReadOnlyList<ChavePosicao> chaves = movimentos
                .Where(m => clienteId is null || m.ClienteId == clienteId)
                .Where(m => instrumentoId is null || m.InstrumentoId == instrumentoId)
                .Select(m => new ChavePosicao(m.ClienteId, m.InstrumentoId))
                .Distinct()
                .OrderBy(c => c.ClienteId)
                .ThenBy(c => c.InstrumentoId)
                .ToList();

            return Result<IReadOnlyList<ChavePosicao>>.Success(chaves);
        };
}
