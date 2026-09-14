namespace Custodia.Application.Precos.Bootstrap;

public sealed record ResultadoColetaDePrecos(
    int Dias,
    int ChamadasHttp,
    int Instrumentos,
    int HistoricoInserido,
    int PrecoAtualCriado,
    int PrecoAtualAtualizado,
    int PrecoAtualCampoTrocado,
    int SemPreco,
    int CampoPosicaoSemPreco,
    int CampoPosicaoNaoInformado,
    int InstrumentosDesconhecidos,
    int ValoresDivergentes,
    int RevisoesMaioresQueZero)
{
    public static readonly ResultadoColetaDePrecos EscopoVazio = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
