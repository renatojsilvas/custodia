namespace Custodia.Application.Precos;

public enum ResultadoAtualizacaoPrecoAtualTipo
{
    Atualizado,
    NaoAtualizado,
}

public enum MotivoPrecoAtualNaoAtualizado
{
    SemLinha,
    CampoDiferenteDoGravado,
    MaisVelhoQueArmazenado,
}

public sealed record ResultadoAtualizacaoPrecoAtual(
    ResultadoAtualizacaoPrecoAtualTipo Tipo, MotivoPrecoAtualNaoAtualizado? Motivo)
{
    public static ResultadoAtualizacaoPrecoAtual Atualizado() =>
        new(ResultadoAtualizacaoPrecoAtualTipo.Atualizado, null);

    public static ResultadoAtualizacaoPrecoAtual NaoAtualizado(MotivoPrecoAtualNaoAtualizado motivo) =>
        new(ResultadoAtualizacaoPrecoAtualTipo.NaoAtualizado, motivo);
}
