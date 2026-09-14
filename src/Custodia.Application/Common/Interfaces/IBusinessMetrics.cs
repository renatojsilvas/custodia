namespace Custodia.Application.Common.Interfaces;

public interface IBusinessMetrics
{
    void RegistrarPosicaoNegativaSinalizada(string clienteId, string instrumentoId, decimal quantidadeResultante);

    void RegistrarHorizonteCalendarioDiasUteis(int diasRestantes, int diasMinimosConfigurados);

    void RegistrarResgateTributadoSobrePrecoMedioProvisorio(string clienteId, string instrumentoId, decimal quantidadeDescoberta);

    void RegistrarLiquidacaoLimitePorTeto(long candidatasEncontradas, long teto);

    void RegistrarLiquidacaoCalendarioExaurido(string clienteId, string tradeId);

    void RegistrarLiquidacaoCandidataInconsistente(string clienteId, string tradeId, string refExternaOfensora);

    void RegistrarGuardaResgatesSemAliq(int quantidade);

    void RegistrarGuardaAjustesDeResgateSemReversao(int quantidade);

    void RegistrarGuardaALiquidarVencidaSemLiquidacao(int quantidade);

    void RegistrarGuardaTributoDivergenteDoRederivado(int quantidade);

    void RegistrarRevisaoDePrecoRecebida(
        string instrumentoId, string campo, DateOnly dataRef, int revisao, string fonte, decimal valorNovo, decimal? valorAnterior);

    void RegistrarValorDivergenteNoHistoricoDePrecos(
        string instrumentoId, string campo, DateOnly dataRef, string fonte, int revisao, decimal valorAnterior, decimal valorNovo);

    void RegistrarPrecoInstrumentoDesconhecido(string instrumentoId);

    void RegistrarPrecoCampoPosicaoNaoInformado(string instrumentoId);

    void RegistrarPrecoInstrumentoPosicionadoSemPreco(string instrumentoId, string motivo);

    void RegistrarConciliacaoDePrecosSemPrecoAtual(int quantidade);

    void RegistrarConciliacaoDePrecosVolta(string desfecho);
}
