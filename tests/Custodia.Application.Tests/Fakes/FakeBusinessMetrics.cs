using Custodia.Application.Common.Interfaces;

namespace Custodia.Application.Tests.Fakes;

internal sealed record SinalizacaoDePosicaoNegativa(string ClienteId, string InstrumentoId, decimal QuantidadeResultante);

internal sealed class FakeBusinessMetrics : IBusinessMetrics
{
    public List<SinalizacaoDePosicaoNegativa> Sinalizacoes { get; } = [];

    public void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante) =>
        Sinalizacoes.Add(new SinalizacaoDePosicaoNegativa(clienteId, instrumentoId, quantidadeResultante));
}
