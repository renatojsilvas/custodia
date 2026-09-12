using Custodia.Domain.Common;

namespace Custodia.Domain.Eventos;

public sealed record MotivoEstacionamento
{
    public static readonly MotivoEstacionamento TipoNaoTratadoPrices = new("tipo_nao_tratado_prices");
    public static readonly MotivoEstacionamento TipoNaoTratadoCorpactions = new("tipo_nao_tratado_corpactions");
    public static readonly MotivoEstacionamento TipoNaoTratadoEod = new("tipo_nao_tratado_eod");
    public static readonly MotivoEstacionamento EstornoOrfaoExpirado = new("estorno_orfao_expirado");
    public static readonly MotivoEstacionamento EstornoClienteDivergente = new("estorno_cliente_divergente");
    public static readonly MotivoEstacionamento EstornoDuplicado = new("estorno_duplicado");
    public static readonly MotivoEstacionamento EstornoDivergente = new("estorno_divergente");
    public static readonly MotivoEstacionamento RetryIndisponivel = new("retry_indisponivel");
    public static readonly MotivoEstacionamento VersaoNaoSuportada = new("versao_nao_suportada");
    public static readonly MotivoEstacionamento PayloadInvalido = new("payload_invalido");
    public static readonly MotivoEstacionamento OrigemRecursoAusente = new("origem_recurso_ausente");
    public static readonly MotivoEstacionamento OrigemRecursoInvalida = new("origem_recurso_invalida");
    public static readonly MotivoEstacionamento IdentificadorComEspacoNaBorda = new("identificador_com_espaco_na_borda");

    public static IReadOnlyCollection<MotivoEstacionamento> All { get; } =
    [
        TipoNaoTratadoPrices,
        TipoNaoTratadoCorpactions,
        TipoNaoTratadoEod,
        EstornoOrfaoExpirado,
        EstornoClienteDivergente,
        EstornoDuplicado,
        EstornoDivergente,
        RetryIndisponivel,
        VersaoNaoSuportada,
        PayloadInvalido,
        OrigemRecursoAusente,
        OrigemRecursoInvalida,
        IdentificadorComEspacoNaBorda,
    ];

    private MotivoEstacionamento(string name) => Name = name;

    public string Name { get; }

    public static Result<MotivoEstacionamento> FromName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var match = All.FirstOrDefault(m => string.Equals(m.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return match is not null
            ? match
            : MotivoEstacionamentoErrors.MotivoInvalido;
    }
}
