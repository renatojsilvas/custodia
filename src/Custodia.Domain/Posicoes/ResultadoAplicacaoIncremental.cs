namespace Custodia.Domain.Posicoes;

public sealed record ResultadoAplicacaoIncremental
{
    private ResultadoAplicacaoIncremental(bool exigeRedobraDaChave, PosicaoTresColunas? estado)
    {
        ExigeRedobraDaChave = exigeRedobraDaChave;
        Estado = estado;
    }

    public bool ExigeRedobraDaChave { get; }

    public PosicaoTresColunas? Estado { get; }

    public static ResultadoAplicacaoIncremental Aplicado(PosicaoTresColunas estado) => new(false, estado);

    public static ResultadoAplicacaoIncremental ExigeRedobra() => new(true, null);
}
