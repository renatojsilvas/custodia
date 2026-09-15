namespace Custodia.Application.Precos;

public enum ResultadoBootstrapPrecoAtualTipo
{
    Criado,
    AtualizadoMesmoCampo,
    CampoTrocado,
    IgnoradoMaisAntigo,
}

public sealed record ResultadoBootstrapPrecoAtual(ResultadoBootstrapPrecoAtualTipo Tipo, string? CampoAnterior)
{
    public static ResultadoBootstrapPrecoAtual Criado() =>
        new(ResultadoBootstrapPrecoAtualTipo.Criado, null);

    public static ResultadoBootstrapPrecoAtual AtualizadoMesmoCampo(string campoAnterior) =>
        new(ResultadoBootstrapPrecoAtualTipo.AtualizadoMesmoCampo, campoAnterior);

    public static ResultadoBootstrapPrecoAtual CampoTrocado(string campoAnterior) =>
        new(ResultadoBootstrapPrecoAtualTipo.CampoTrocado, campoAnterior);

    public static ResultadoBootstrapPrecoAtual IgnoradoMaisAntigo() =>
        new(ResultadoBootstrapPrecoAtualTipo.IgnoradoMaisAntigo, null);
}

public sealed record ResultadoRegistroBootstrap(ResultadoHistorico Historico, ResultadoBootstrapPrecoAtual? PrecoAtual);
