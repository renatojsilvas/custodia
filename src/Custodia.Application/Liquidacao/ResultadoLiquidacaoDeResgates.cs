namespace Custodia.Application.Liquidacao;

public enum DesfechoLiquidacaoDeResgates
{
    Completude,
    DadoQuebrado,
}

public sealed record ResultadoLiquidacaoDeResgates(
    DesfechoLiquidacaoDeResgates Desfecho,
    int CandidatasExaminadas,
    int FatosLiquidados,
    int FatosNaoVencidos,
    int FatosJaTratados,
    int FatosInconsistentes);
