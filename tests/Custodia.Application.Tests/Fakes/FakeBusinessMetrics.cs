using Custodia.Application.Common.Interfaces;

namespace Custodia.Application.Tests.Fakes;

internal sealed record SinalizacaoDePosicaoNegativa(string ClienteId, string InstrumentoId, decimal QuantidadeResultante);

internal sealed record RegistroDeHorizonteCalendario(int DiasRestantes, int DiasMinimosConfigurados);

internal sealed record SinalizacaoDeResgateSobrePrecoMedioProvisorio(
    string ClienteId, string InstrumentoId, decimal QuantidadeDescoberta);

internal sealed class FakeBusinessMetrics : IBusinessMetrics
{
    public List<SinalizacaoDePosicaoNegativa> Sinalizacoes { get; } = [];

    public List<RegistroDeHorizonteCalendario> RegistrosDeHorizonteCalendario { get; } = [];

    public List<SinalizacaoDeResgateSobrePrecoMedioProvisorio> SinalizacoesDeResgateSobrePrecoMedioProvisorio { get; } = [];

    public void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante) =>
        Sinalizacoes.Add(new SinalizacaoDePosicaoNegativa(clienteId, instrumentoId, quantidadeResultante));

    public void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados) =>
        RegistrosDeHorizonteCalendario.Add(new RegistroDeHorizonteCalendario(diasRestantes, diasMinimosConfigurados));

    public void RegistrarResgateTributadoSobrePrecoMedioProvisorio(
        string clienteId, string instrumentoId, decimal quantidadeDescoberta) =>
        SinalizacoesDeResgateSobrePrecoMedioProvisorio.Add(
            new SinalizacaoDeResgateSobrePrecoMedioProvisorio(clienteId, instrumentoId, quantidadeDescoberta));
}
