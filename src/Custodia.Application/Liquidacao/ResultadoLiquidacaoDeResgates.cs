namespace Custodia.Application.Liquidacao;

public enum DesfechoLiquidacaoDeResgates
{
    Completude,
}

public sealed record ResultadoLiquidacaoDeResgates(
    DesfechoLiquidacaoDeResgates Desfecho,
    int CandidatasExaminadas,
    int FatosLiquidados,
    int FatosNaoVencidos,
    int FatosJaTratados);
