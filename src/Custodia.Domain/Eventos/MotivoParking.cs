using Custodia.Domain.Common;

namespace Custodia.Domain.Eventos;

public sealed record MotivoParking
{
    public static readonly MotivoParking TipoNaoTratadoPrices = new("tipo_nao_tratado_prices");
    public static readonly MotivoParking TipoNaoTratadoCorpactions = new("tipo_nao_tratado_corpactions");
    public static readonly MotivoParking TipoNaoTratadoEod = new("tipo_nao_tratado_eod");
    public static readonly MotivoParking EstornoOrfaoExpirado = new("estorno_orfao_expirado");
    public static readonly MotivoParking EstornoClienteDivergente = new("estorno_cliente_divergente");
    public static readonly MotivoParking EstornoDuplicado = new("estorno_duplicado");
    public static readonly MotivoParking EstornoDivergente = new("estorno_divergente");
    public static readonly MotivoParking RetryIndisponivel = new("retry_indisponivel");
    public static readonly MotivoParking VersaoNaoSuportada = new("versao_nao_suportada");
    public static readonly MotivoParking PayloadInvalido = new("payload_invalido");
    public static readonly MotivoParking OrigemRecursoAusente = new("origem_recurso_ausente");
    public static readonly MotivoParking OrigemRecursoInvalida = new("origem_recurso_invalida");
    public static readonly MotivoParking IdentificadorComEspacoNaBorda = new("identificador_com_espaco_na_borda");

    public static IReadOnlyCollection<MotivoParking> All { get; } =
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

    private MotivoParking(string name) => Name = name;

    public string Name { get; }

    public static Result<MotivoParking> FromName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var match = All.FirstOrDefault(m => string.Equals(m.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        return match is not null
            ? match
            : MotivoParkingErrors.MotivoInvalido;
    }
}
