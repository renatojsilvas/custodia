using Custodia.Application.Movimentos;
using Custodia.Domain.Common;
using Npgsql;

namespace Custodia.Infrastructure.Persistence;

internal static class PostgresExceptionTranslator
{
    private const string TriggerDataEventoFutura = "movimentos_bloqueia_data_futura";
    private const string TriggerImutavel = "movimentos_bloqueia_update_delete";

    public static Error? Traduzir(PostgresException pg)
    {
        ArgumentNullException.ThrowIfNull(pg);

        if (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == "ix_movimentos_ref_estorno_unico")
        {
            return MovimentoWriteErrors.RefEstornoDuplicado;
        }

        if (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == "ix_movimentos_cliente_ref_externa")
        {
            return MovimentoWriteErrors.MensagemDuplicada;
        }

        if (pg.SqlState == PostgresErrorCodes.CheckViolation && EhCheckDeEspacoNaBorda(pg.ConstraintName))
        {
            return MovimentoWriteErrors.IdentificadorComEspacoNaBorda;
        }

        if (pg.SqlState == PostgresErrorCodes.NumericValueOutOfRange)
        {
            return MovimentoWriteErrors.ValorNumericoExcedeMagnitudeOuEscalaSuportada;
        }

        if (pg.SqlState == PostgresErrorCodes.RaiseException && ContemFuncao(pg.Where, TriggerDataEventoFutura))
        {
            return MovimentoWriteErrors.DataEventoFutura;
        }

        if (pg.SqlState == PostgresErrorCodes.RaiseException && ContemFuncao(pg.Where, TriggerImutavel))
        {
            return MovimentoWriteErrors.OperacaoNaoPermitidaSobreMovimentoImutavel;
        }

        return null;
    }

    private static bool EhCheckDeEspacoNaBorda(string? constraintName) =>
        constraintName is "ck_movimentos_cliente_id_sem_espaco_nas_bordas"
            or "ck_movimentos_instrumento_id_sem_espaco_nas_bordas"
            or "ck_movimentos_ref_externa_sem_espaco_nas_bordas";

    private static bool ContemFuncao(string? where, string nomeDaFuncao) =>
        where is not null && where.Contains(nomeDaFuncao, StringComparison.Ordinal);
}
