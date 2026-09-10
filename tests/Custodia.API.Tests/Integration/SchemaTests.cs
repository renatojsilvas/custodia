using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Custodia.Domain.Common;
using Custodia.Domain.Movimentos;
using Dapper;
using Npgsql;

namespace Custodia.API.Tests.Integration;

[Collection("api")]
public sealed class SchemaTests
{
    private static readonly DateOnly DataPassadaPadrao = new(2020, 1, 1);

    static SchemaTests()
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

        public override void SetValue(IDbDataParameter parameter, DateOnly value) =>
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    private readonly string _connectionString;

    public SchemaTests(ApiTestFactory factory)
    {
        _ = factory;
        _connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection não foi definida pela ApiTestFactory.");
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string NovoClienteId() => $"cliente-schema-tests-{Guid.NewGuid():N}";

    private static string NovaRefExterna() => $"ref-schema-tests-{Guid.NewGuid():N}";

    private sealed record ColumnInfo(
        string ColumnName,
        string DataType,
        string IsNullable,
        string? ColumnDefault,
        int? NumericPrecision,
        int? NumericScale);

    private async Task<IReadOnlyDictionary<string, ColumnInfo>> GetColumnsAsync(
        NpgsqlConnection connection, string tableName)
    {
        var rows = await connection.QueryAsync<ColumnInfo>(
            """
            SELECT column_name AS "ColumnName",
                   data_type AS "DataType",
                   is_nullable AS "IsNullable",
                   column_default AS "ColumnDefault",
                   numeric_precision AS "NumericPrecision",
                   numeric_scale AS "NumericScale"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @tableName
            """,
            new { tableName });
        return rows.ToDictionary(r => r.ColumnName);
    }

    private async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName)
    {
        return await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @tableName
            )
            """,
            new { tableName });
    }

    private sealed class ForeignKeyRow
    {
        public char ConfDelType { get; set; }
        public string[] ColunasOrigem { get; set; } = [];
        public string[] ColunasReferenciadas { get; set; } = [];
    }

    private sealed record ForeignKeyInfo(
        char ConfDelType,
        IReadOnlyList<string> ColunasOrigem,
        string TabelaReferenciada,
        IReadOnlyList<string> ColunasReferenciadas);

    private async Task<ForeignKeyInfo?> GetForeignKeyAsync(
        NpgsqlConnection connection, string tableName, string referencedTableName)
    {
        var row = await connection.QuerySingleOrDefaultAsync<ForeignKeyRow>(
            """
            SELECT
                con.confdeltype AS "ConfDelType",
                (SELECT array_agg(a.attname ORDER BY k.ord)
                 FROM unnest(con.conkey) WITH ORDINALITY AS k(attnum, ord)
                 JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum) AS "ColunasOrigem",
                (SELECT array_agg(a.attname ORDER BY k.ord)
                 FROM unnest(con.confkey) WITH ORDINALITY AS k(attnum, ord)
                 JOIN pg_attribute a ON a.attrelid = con.confrelid AND a.attnum = k.attnum) AS "ColunasReferenciadas"
            FROM pg_constraint con
            JOIN pg_class src ON src.oid = con.conrelid
            JOIN pg_class dst ON dst.oid = con.confrelid
            WHERE con.contype = 'f'
              AND src.relname = @tableName
              AND dst.relname = @referencedTableName
            """,
            new { tableName, referencedTableName });
        return row is null
            ? null
            : new ForeignKeyInfo(row.ConfDelType, row.ColunasOrigem, referencedTableName, row.ColunasReferenciadas);
    }

    private void AssertColumn(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string tableName,
        string columnName,
        string expectedDataType,
        bool expectedNullable,
        bool expectNoDefault = true)
    {
        Assert.True(
            columns.ContainsKey(columnName),
            $"{tableName}: coluna '{columnName}' não existe. Colunas encontradas: [{string.Join(", ", columns.Keys)}]");
        var column = columns[columnName];
        Assert.True(
            string.Equals(column.DataType, expectedDataType, StringComparison.Ordinal),
            $"{tableName}.{columnName}: tipo esperado '{expectedDataType}', encontrado '{column.DataType}'.");
        var expectedIsNullableFlag = expectedNullable ? "YES" : "NO";
        Assert.True(
            string.Equals(column.IsNullable, expectedIsNullableFlag, StringComparison.Ordinal),
            $"{tableName}.{columnName}: nullability esperada '{expectedIsNullableFlag}', encontrada '{column.IsNullable}'.");
        if (expectNoDefault)
        {
            Assert.True(
                column.ColumnDefault is null,
                $"{tableName}.{columnName}: esperava column_default IS NULL, mas encontrou default '{column.ColumnDefault}'.");
        }
    }

    private async Task<long> InserirMovimentoBrutoAsync(
        NpgsqlConnection connection,
        string? clienteId,
        string? instrumentoId,
        string tipo,
        DateOnly dataEvento,
        decimal qtdDelta,
        decimal valorFinanceiro,
        string? refExterna,
        long? refEstorno = null,
        DbTransaction? transaction = null)
    {
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, qtd_delta, valor_financeiro, ref_externa, ref_estorno)
            VALUES (@clienteId, @instrumentoId, @tipo, @dataEvento, @qtdDelta, @valorFinanceiro, @refExterna, @refEstorno)
            RETURNING id
            """,
            new { clienteId, instrumentoId, tipo, dataEvento, qtdDelta, valorFinanceiro, refExterna, refEstorno },
            transaction));
    }

    private Task<long> InserirMovimentoAsync(
        NpgsqlConnection connection,
        string? clienteId = null,
        string instrumentoId = "td:tesouro-selic-2029",
        string tipo = "compra",
        DateOnly? dataEvento = null,
        decimal qtdDelta = 10m,
        decimal valorFinanceiro = 1000m,
        string? refExterna = null,
        long? refEstorno = null,
        DbTransaction? transaction = null)
        => InserirMovimentoBrutoAsync(
            connection,
            clienteId ?? NovoClienteId(),
            instrumentoId,
            tipo,
            dataEvento ?? DataPassadaPadrao,
            qtdDelta,
            valorFinanceiro,
            refExterna ?? NovaRefExterna(),
            refEstorno,
            transaction);

    private Task<DateOnly> ObterDataDeHojeEmSaoPauloAsync(NpgsqlConnection connection)
        => connection.ExecuteScalarAsync<DateOnly>(
            "SELECT (now() AT TIME ZONE 'America/Sao_Paulo')::date");

    private sealed record LinhaMovimento(
        long Id, string Tipo, string InstrumentoId, decimal QtdDelta, decimal ValorFinanceiro, DateOnly DataEvento, string RefExterna);

    private async Task<IReadOnlyDictionary<long, LinhaMovimento>> LerLinhasGravadasPorIdAsync(
        NpgsqlConnection connection, IReadOnlyList<long> ids)
    {
        var linhas = await connection.QueryAsync<LinhaMovimento>(
            """
            SELECT id AS "Id",
                   tipo AS "Tipo",
                   instrumento_id AS "InstrumentoId",
                   qtd_delta AS "QtdDelta",
                   valor_financeiro AS "ValorFinanceiro",
                   data_evento AS "DataEvento",
                   ref_externa AS "RefExterna"
            FROM movimentos
            WHERE id = ANY(@ids)
            """,
            new { ids = ids.ToArray() });
        return linhas.ToDictionary(l => l.Id);
    }

    private sealed record LinhasDeResgate(
        string ClienteId,
        string InstrumentoId,
        LinhaMovimento Venda,
        LinhaMovimento Ir,
        LinhaMovimento Iof,
        LinhaMovimento Aliq,
        LinhaMovimento LiqAliq,
        LinhaMovimento LiqBrl)
    {
        public IReadOnlyList<LinhaMovimento> Todas => [Venda, Ir, Iof, Aliq, LiqAliq, LiqBrl];
    }

    private sealed record LinhasDeVencimento(
        string ClienteId,
        string InstrumentoId,
        LinhaMovimento Principal,
        LinhaMovimento Ir,
        LinhaMovimento Iof,
        LinhaMovimento Aliq,
        LinhaMovimento LiqAliq,
        LinhaMovimento LiqBrl)
    {
        public IReadOnlyList<LinhaMovimento> Todas => [Principal, Ir, Iof, Aliq, LiqAliq, LiqBrl];
    }

    private sealed record LinhasDeCupom(
        string ClienteId,
        string InstrumentoId,
        LinhaMovimento Principal,
        LinhaMovimento Ir,
        LinhaMovimento Aliq,
        LinhaMovimento LiqAliq,
        LinhaMovimento LiqBrl)
    {
        public IReadOnlyList<LinhaMovimento> Todas => [Principal, Ir, Aliq, LiqAliq, LiqBrl];
    }

    private async Task<LinhasDeResgate> InserirResgateCompletoAsync(
        NpgsqlConnection connection, string tradeId, decimal quantidade, decimal valorBruto, decimal ir, decimal iof)
    {
        var clienteId = NovoClienteId();
        var instrumentoId = $"td:tesouro-resgate-schema-tests-{Guid.NewGuid():N}";
        var hoje = await ObterDataDeHojeEmSaoPauloAsync(connection);
        var d = hoje.AddDays(-2);
        var d1 = hoje.AddDays(-1);
        var liquido = valorBruto - ir - iof;

        await using var transacao = await connection.BeginTransactionAsync();

        var idVenda = await InserirMovimentoBrutoAsync(
            connection, clienteId, instrumentoId, TipoMovimento.Venda.Name, d, -quantidade, valorBruto, tradeId, transaction: transacao);
        var idIr = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido.Name, d, -ir, ir, $"ir:{tradeId}", transaction: transacao);
        var idIof = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Iof.Name, d, -iof, iof, $"iof:{tradeId}", transaction: transacao);
        var idAliq = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar.Name, d, valorBruto, valorBruto, $"aliq:{tradeId}", transaction: transacao);
        var idLiqAliq = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao.Name, d1, -liquido, liquido, $"liq:{tradeId}:aliq", transaction: transacao);
        var idLiqBrl = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao.Name, d1, liquido, liquido, $"liq:{tradeId}:brl", transaction: transacao);

        await transacao.CommitAsync();

        var linhasGravadas = await LerLinhasGravadasPorIdAsync(
            connection, [idVenda, idIr, idIof, idAliq, idLiqAliq, idLiqBrl]);

        return new LinhasDeResgate(
            clienteId,
            instrumentoId,
            linhasGravadas[idVenda],
            linhasGravadas[idIr],
            linhasGravadas[idIof],
            linhasGravadas[idAliq],
            linhasGravadas[idLiqAliq],
            linhasGravadas[idLiqBrl]);
    }

    private async Task<LinhasDeVencimento> InserirVencimentoCompletoAsync(
        NpgsqlConnection connection, decimal quantidade, decimal precoFinal, decimal ir, decimal iof)
    {
        var clienteId = NovoClienteId();
        var instrumentoId = $"td:tesouro-vencimento-schema-tests-{Guid.NewGuid():N}";
        var hoje = await ObterDataDeHojeEmSaoPauloAsync(connection);
        var d = hoje.AddDays(-2);
        var d1 = hoje.AddDays(-1);
        var fato = $"venc:{instrumentoId}:{d:yyyy-MM-dd}";
        var valorBruto = quantidade * precoFinal;
        var liquido = valorBruto - ir - iof;

        await using var transacao = await connection.BeginTransactionAsync();

        var idPrincipal = await InserirMovimentoBrutoAsync(
            connection, clienteId, instrumentoId, TipoMovimento.Resgate.Name, d, -quantidade, valorBruto, fato, transaction: transacao);
        var idIr = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido.Name, d, -ir, ir, $"ir:{fato}", transaction: transacao);
        var idIof = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Iof.Name, d, -iof, iof, $"iof:{fato}", transaction: transacao);
        var idAliq = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar.Name, d, valorBruto, valorBruto, $"aliq:{fato}", transaction: transacao);
        var idLiqAliq = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao.Name, d1, -liquido, liquido, $"liq:{fato}:aliq", transaction: transacao);
        var idLiqBrl = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao.Name, d1, liquido, liquido, $"liq:{fato}:brl", transaction: transacao);

        await transacao.CommitAsync();

        var linhasGravadas = await LerLinhasGravadasPorIdAsync(
            connection, [idPrincipal, idIr, idIof, idAliq, idLiqAliq, idLiqBrl]);

        return new LinhasDeVencimento(
            clienteId,
            instrumentoId,
            linhasGravadas[idPrincipal],
            linhasGravadas[idIr],
            linhasGravadas[idIof],
            linhasGravadas[idAliq],
            linhasGravadas[idLiqAliq],
            linhasGravadas[idLiqBrl]);
    }

    private async Task<LinhasDeCupom> InserirCupomCompletoAsync(
        NpgsqlConnection connection, decimal quantidade, decimal valorPorUnidade, decimal ir)
    {
        var clienteId = NovoClienteId();
        var instrumentoId = $"td:tesouro-cupom-schema-tests-{Guid.NewGuid():N}";
        var hoje = await ObterDataDeHojeEmSaoPauloAsync(connection);
        var d = hoje.AddDays(-2);
        var d1 = hoje.AddDays(-1);
        var fato = $"cupom:{instrumentoId}:{d:yyyy-MM-dd}";
        var valorBruto = quantidade * valorPorUnidade;
        var liquido = valorBruto - ir;

        await using var transacao = await connection.BeginTransactionAsync();

        var idPrincipal = await InserirMovimentoBrutoAsync(
            connection, clienteId, instrumentoId, TipoMovimento.Cupom.Name, d, 0m, valorBruto, fato, transaction: transacao);
        var idIr = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.IrRetido.Name, d, -ir, ir, $"ir:{fato}", transaction: transacao);
        var idAliq = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.ALiquidar.Name, d, liquido, liquido, $"aliq:{fato}", transaction: transacao);
        var idLiqAliq = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.ALiquidar, TipoMovimento.Liquidacao.Name, d1, -liquido, liquido, $"liq:{fato}:aliq", transaction: transacao);
        var idLiqBrl = await InserirMovimentoBrutoAsync(
            connection, clienteId, InstrumentosCaixa.Brl, TipoMovimento.Liquidacao.Name, d1, liquido, liquido, $"liq:{fato}:brl", transaction: transacao);

        await transacao.CommitAsync();

        var linhasGravadas = await LerLinhasGravadasPorIdAsync(
            connection, [idPrincipal, idIr, idAliq, idLiqAliq, idLiqBrl]);

        return new LinhasDeCupom(
            clienteId,
            instrumentoId,
            linhasGravadas[idPrincipal],
            linhasGravadas[idIr],
            linhasGravadas[idAliq],
            linhasGravadas[idLiqAliq],
            linhasGravadas[idLiqBrl]);
    }

    [Theory]
    [InlineData("movimentos")]
    [InlineData("posicao_corrente")]
    [InlineData("preco_atual")]
    [InlineData("historico_precos")]
    [InlineData("snapshots_posicao")]
    public async Task AsCincoTabelasDaSete1Existem(string tabela)
    {
        using var connection = await OpenConnectionAsync();

        Assert.True(await TableExistsAsync(connection, tabela), $"Tabela '{tabela}' não existe no schema public.");
    }

    [Fact]
    public async Task Movimentos_ColunasBatemComA71EOsQuatroDesviosPorCorrecao()
    {
        using var connection = await OpenConnectionAsync();

        var columns = await GetColumnsAsync(connection, "movimentos");
        var esperadas = new[]
        {
            "id", "cliente_id", "instrumento_id", "tipo", "data_evento", "registrado_em",
            "qtd_delta", "valor_financeiro", "ref_externa", "ref_estorno",
        };
        Assert.True(
            esperadas.ToHashSet().SetEquals(columns.Keys),
            "movimentos: conjunto de colunas divergente. Esperado: " +
            $"[{string.Join(", ", esperadas)}], encontrado: [{string.Join(", ", columns.Keys)}].");

        AssertColumn(columns, "movimentos", "id", "bigint", expectedNullable: false);
        AssertColumn(columns, "movimentos", "cliente_id", "text", expectedNullable: false);
        AssertColumn(columns, "movimentos", "instrumento_id", "text", expectedNullable: false);
        AssertColumn(columns, "movimentos", "tipo", "text", expectedNullable: false);
        AssertColumn(columns, "movimentos", "data_evento", "date", expectedNullable: false);
        AssertColumn(columns, "movimentos", "qtd_delta", "numeric", expectedNullable: false);
        AssertColumn(columns, "movimentos", "valor_financeiro", "numeric", expectedNullable: false);
        AssertColumn(columns, "movimentos", "ref_externa", "text", expectedNullable: false);
        AssertColumn(columns, "movimentos", "ref_estorno", "bigint", expectedNullable: true);
    }

    [Fact]
    public async Task Movimentos_RegistradoEm_TemDefaultNow()
    {
        using var connection = await OpenConnectionAsync();

        var columns = await GetColumnsAsync(connection, "movimentos");
        AssertColumn(columns, "movimentos", "registrado_em", "timestamp with time zone", expectedNullable: false, expectNoDefault: false);
        var registradoEm = columns["registrado_em"];
        Assert.True(
            registradoEm.ColumnDefault is not null && registradoEm.ColumnDefault.Contains("now()", StringComparison.OrdinalIgnoreCase),
            $"movimentos.registrado_em: esperava column_default contendo 'now()', encontrado '{registradoEm.ColumnDefault}'.");
    }

    [Fact]
    public async Task Movimentos_ChavePrimaria_EhSomenteId()
    {
        using var connection = await OpenConnectionAsync();

        var pk = await connection.QueryAsync<string>(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE c.relname = 'movimentos' AND i.indisprimary
            """);
        Assert.Equal(new[] { "id" }, pk);
    }

    [Fact]
    public async Task PosicaoCorrente_ColunasBatemComA71()
    {
        using var connection = await OpenConnectionAsync();

        var columns = await GetColumnsAsync(connection, "posicao_corrente");
        var esperadas = new[] { "cliente_id", "instrumento_id", "quantidade", "preco_medio", "custo_total" };
        Assert.True(
            esperadas.ToHashSet().SetEquals(columns.Keys),
            "posicao_corrente: conjunto de colunas divergente. Esperado: " +
            $"[{string.Join(", ", esperadas)}], encontrado: [{string.Join(", ", columns.Keys)}].");

        AssertColumn(columns, "posicao_corrente", "cliente_id", "text", expectedNullable: false);
        AssertColumn(columns, "posicao_corrente", "instrumento_id", "text", expectedNullable: false);
        AssertColumn(columns, "posicao_corrente", "quantidade", "numeric", expectedNullable: false);
        AssertColumn(columns, "posicao_corrente", "preco_medio", "numeric", expectedNullable: true);
        AssertColumn(columns, "posicao_corrente", "custo_total", "numeric", expectedNullable: true);
    }

    [Fact]
    public async Task PosicaoCorrente_ChavePrimaria_EhClienteIdEInstrumentoId()
    {
        using var connection = await OpenConnectionAsync();

        var pk = await connection.QueryAsync<string>(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE c.relname = 'posicao_corrente' AND i.indisprimary
            ORDER BY array_position(i.indkey, a.attnum)
            """);
        Assert.Equal(new[] { "cliente_id", "instrumento_id" }, pk);
    }

    [Fact]
    public async Task PrecoAtual_ColunasBatemComA71SemColunaFonte()
    {
        using var connection = await OpenConnectionAsync();

        var columns = await GetColumnsAsync(connection, "preco_atual");
        var esperadas = new[] { "instrumento_id", "data_ref", "campo", "valor", "revisao" };
        Assert.True(
            esperadas.ToHashSet().SetEquals(columns.Keys),
            "preco_atual: conjunto de colunas divergente. Esperado: " +
            $"[{string.Join(", ", esperadas)}], encontrado: [{string.Join(", ", columns.Keys)}].");
        Assert.False(
            columns.ContainsKey("fonte"),
            "preco_atual não deve ter coluna 'fonte' (decisão 5c) — quem guarda todos os campos, com a chave inteira, é historico_precos.");

        AssertColumn(columns, "preco_atual", "instrumento_id", "text", expectedNullable: false);
        AssertColumn(columns, "preco_atual", "data_ref", "date", expectedNullable: false);
        AssertColumn(columns, "preco_atual", "campo", "text", expectedNullable: false);
        AssertColumn(columns, "preco_atual", "valor", "numeric", expectedNullable: false);
        AssertColumn(columns, "preco_atual", "revisao", "integer", expectedNullable: false);
    }

    [Fact]
    public async Task PrecoAtual_ChavePrimaria_EhSomenteInstrumentoId()
    {
        using var connection = await OpenConnectionAsync();

        var pk = await connection.QueryAsync<string>(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE c.relname = 'preco_atual' AND i.indisprimary
            """);
        Assert.Equal(new[] { "instrumento_id" }, pk);
    }

    [Fact]
    public async Task HistoricoPrecos_ColunasBatemComA71EComRevisaoDefaultZeroEObservadoEmDefaultNow()
    {
        using var connection = await OpenConnectionAsync();

        var columns = await GetColumnsAsync(connection, "historico_precos");
        var esperadas = new[] { "instrumento_id", "data_ref", "campo", "fonte", "revisao", "valor", "observado_em" };
        Assert.True(
            esperadas.ToHashSet().SetEquals(columns.Keys),
            "historico_precos: conjunto de colunas divergente. Esperado: " +
            $"[{string.Join(", ", esperadas)}], encontrado: [{string.Join(", ", columns.Keys)}].");

        AssertColumn(columns, "historico_precos", "instrumento_id", "text", expectedNullable: false);
        AssertColumn(columns, "historico_precos", "data_ref", "date", expectedNullable: false);
        AssertColumn(columns, "historico_precos", "campo", "text", expectedNullable: false);
        AssertColumn(columns, "historico_precos", "fonte", "text", expectedNullable: false);
        AssertColumn(columns, "historico_precos", "valor", "numeric", expectedNullable: false);

        var revisao = columns["revisao"];
        Assert.Equal("integer", revisao.DataType);
        Assert.Equal("NO", revisao.IsNullable);
        Assert.True(
            revisao.ColumnDefault is not null && revisao.ColumnDefault.Contains('0'),
            $"historico_precos.revisao: esperava column_default contendo '0', encontrado '{revisao.ColumnDefault}'.");

        var observadoEm = columns["observado_em"];
        Assert.Equal("timestamp with time zone", observadoEm.DataType);
        Assert.Equal("NO", observadoEm.IsNullable);
        Assert.True(
            observadoEm.ColumnDefault is not null && observadoEm.ColumnDefault.Contains("now()", StringComparison.OrdinalIgnoreCase),
            $"historico_precos.observado_em: esperava column_default contendo 'now()', encontrado '{observadoEm.ColumnDefault}'.");
    }

    [Fact]
    public async Task HistoricoPrecos_ChavePrimaria_EhInstrumentoIdDataRefCampoFonteRevisaoNestaOrdem()
    {
        using var connection = await OpenConnectionAsync();

        var pk = await connection.QueryAsync<string>(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE c.relname = 'historico_precos' AND i.indisprimary
            ORDER BY array_position(i.indkey, a.attnum)
            """);
        Assert.Equal(new[] { "instrumento_id", "data_ref", "campo", "fonte", "revisao" }, pk);
    }

    [Fact]
    public async Task SnapshotsPosicao_ColunasBatemComA71ComVigenteDefaultTrueECalculadoEmDefaultNow()
    {
        using var connection = await OpenConnectionAsync();

        var columns = await GetColumnsAsync(connection, "snapshots_posicao");
        var esperadas = new[]
        {
            "cliente_id", "instrumento_id", "data", "calculado_em", "quantidade",
            "preco", "valor", "preco_medio", "custo", "vigente",
        };
        Assert.True(
            esperadas.ToHashSet().SetEquals(columns.Keys),
            "snapshots_posicao: conjunto de colunas divergente. Esperado: " +
            $"[{string.Join(", ", esperadas)}], encontrado: [{string.Join(", ", columns.Keys)}].");

        AssertColumn(columns, "snapshots_posicao", "cliente_id", "text", expectedNullable: false);
        AssertColumn(columns, "snapshots_posicao", "instrumento_id", "text", expectedNullable: false);
        AssertColumn(columns, "snapshots_posicao", "data", "date", expectedNullable: false);
        AssertColumn(columns, "snapshots_posicao", "quantidade", "numeric", expectedNullable: false);
        AssertColumn(columns, "snapshots_posicao", "preco", "numeric", expectedNullable: false);
        AssertColumn(columns, "snapshots_posicao", "valor", "numeric", expectedNullable: false);
        AssertColumn(columns, "snapshots_posicao", "preco_medio", "numeric", expectedNullable: true);
        AssertColumn(columns, "snapshots_posicao", "custo", "numeric", expectedNullable: true);

        var vigente = columns["vigente"];
        Assert.Equal("boolean", vigente.DataType);
        Assert.Equal("NO", vigente.IsNullable);
        Assert.True(
            vigente.ColumnDefault is not null && vigente.ColumnDefault.Contains("true", StringComparison.OrdinalIgnoreCase),
            $"snapshots_posicao.vigente: esperava column_default contendo 'true', encontrado '{vigente.ColumnDefault}'.");

        var calculadoEm = columns["calculado_em"];
        Assert.Equal("timestamp with time zone", calculadoEm.DataType);
        Assert.Equal("NO", calculadoEm.IsNullable);
        Assert.True(
            calculadoEm.ColumnDefault is not null && calculadoEm.ColumnDefault.Contains("now()", StringComparison.OrdinalIgnoreCase),
            $"snapshots_posicao.calculado_em: esperava column_default contendo 'now()', encontrado '{calculadoEm.ColumnDefault}'.");
    }

    [Fact]
    public async Task SnapshotsPosicao_ChavePrimaria_EhClienteIdInstrumentoIdDataCalculadoEmNestaOrdem()
    {
        using var connection = await OpenConnectionAsync();

        var pk = await connection.QueryAsync<string>(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE c.relname = 'snapshots_posicao' AND i.indisprimary
            ORDER BY array_position(i.indkey, a.attnum)
            """);
        Assert.Equal(new[] { "cliente_id", "instrumento_id", "data", "calculado_em" }, pk);
    }

    [Fact]
    public async Task Movimentos_Insert_ControlePositivo_EhAceito()
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(connection);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Movimentos_Update_EhBloqueadoPelaTriggerAppendOnly()
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(connection);
        var exception = await Record.ExceptionAsync(() => connection.ExecuteAsync(
            "UPDATE movimentos SET qtd_delta = 99 WHERE id = @id", new { id }));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Contains("append-only", pgException.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Movimentos_Delete_EhBloqueadoPelaTriggerAppendOnly()
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(connection);
        var exception = await Record.ExceptionAsync(() => connection.ExecuteAsync(
            "DELETE FROM movimentos WHERE id = @id", new { id }));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Contains("append-only", pgException.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Movimentos_Insert_AjusteComRefEstornoNulo_EhRecusado()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, tipo: TipoMovimento.Ajuste.Name, qtdDelta: -1m, valorFinanceiro: -100m, refEstorno: null));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_ajuste_coerente", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_AjusteApontandoParaMovimentoDeOutroCliente_EhRecusado()
    {
        using var connection = await OpenConnectionAsync();

        var instrumentoId = "td:tesouro-selic-2029";
        var original = await InserirMovimentoAsync(
            connection, clienteId: NovoClienteId(), instrumentoId: instrumentoId, tipo: TipoMovimento.Compra.Name);
        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, clienteId: NovoClienteId(), instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -1000m, refEstorno: original));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, pgException.SqlState);
        Assert.Equal("FK_movimentos_movimentos_ref_estorno", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_AjusteApontandoParaMovimentoDoMesmoClienteEOutroInstrumento_EhRecusado()
    {
        using var connection = await OpenConnectionAsync();

        var clienteId = NovoClienteId();
        var original = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: "td:tesouro-selic-2029", tipo: TipoMovimento.Compra.Name);
        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: "td:tesouro-ipca-2035", tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -1000m, refEstorno: original));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, pgException.SqlState);
        Assert.Equal("FK_movimentos_movimentos_ref_estorno", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_FkRefEstorno_ApontaParaIdClienteIdInstrumentoIdNaOrdemCertaComRestrict()
    {
        using var connection = await OpenConnectionAsync();

        var fk = await GetForeignKeyAsync(connection, "movimentos", "movimentos");
        Assert.True(fk is not null, "movimentos: não encontrei FK auto-referente de ref_estorno.");
        Assert.Equal(new[] { "ref_estorno", "cliente_id", "instrumento_id" }, fk!.ColunasOrigem);
        Assert.Equal(new[] { "id", "cliente_id", "instrumento_id" }, fk.ColunasReferenciadas);
        Assert.True(
            fk.ConfDelType == 'r',
            $"movimentos.(ref_estorno, cliente_id, instrumento_id) -> movimentos(id, cliente_id, instrumento_id): " +
            $"esperava ON DELETE RESTRICT (confdeltype = 'r'), encontrado '{fk.ConfDelType}'.");
    }

    [Fact]
    public async Task Movimentos_Insert_RefEstornoIgualAoProprioId_EhRecusado()
    {
        using var connection = await OpenConnectionAsync();

        var proximoId = await connection.ExecuteScalarAsync<long>(
            "SELECT nextval(pg_get_serial_sequence('movimentos', 'id'))");
        var exception = await Record.ExceptionAsync(() => connection.ExecuteAsync(
            """
            INSERT INTO movimentos (id, cliente_id, instrumento_id, tipo, data_evento, qtd_delta, valor_financeiro, ref_externa, ref_estorno)
            VALUES (@id, @clienteId, @instrumentoId, 'ajuste', @dataEvento, -1, -100, @refExterna, @id)
            """,
            new
            {
                id = proximoId,
                clienteId = NovoClienteId(),
                instrumentoId = "td:tesouro-selic-2029",
                dataEvento = DataPassadaPadrao,
                refExterna = NovaRefExterna(),
            }));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_estorno_nao_auto", pgException.ConstraintName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Movimentos_Insert_RefExternaNulaVaziaOuSoEspacos_EhRecusado(string? refExterna)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoBrutoAsync(
            connection, NovoClienteId(), "td:tesouro-selic-2029", TipoMovimento.Compra.Name, DataPassadaPadrao, 10m, 1000m, refExterna));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        if (refExterna is null)
        {
            Assert.Equal(PostgresErrorCodes.NotNullViolation, pgException.SqlState);
        }
        else
        {
            Assert.Equal("ck_movimentos_ref_externa_nao_vazia", pgException.ConstraintName);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Movimentos_Insert_ClienteIdNuloVazioOuSoEspacos_EhRecusado(string? clienteId)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoBrutoAsync(
            connection, clienteId, "td:tesouro-selic-2029", TipoMovimento.Compra.Name, DataPassadaPadrao, 10m, 1000m, NovaRefExterna()));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        if (clienteId is null)
        {
            Assert.Equal(PostgresErrorCodes.NotNullViolation, pgException.SqlState);
        }
        else
        {
            Assert.Equal("ck_movimentos_cliente_id_nao_vazio", pgException.ConstraintName);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Movimentos_Insert_InstrumentoIdNuloVazioOuSoEspacos_EhRecusado(string? instrumentoId)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoBrutoAsync(
            connection, NovoClienteId(), instrumentoId, TipoMovimento.Compra.Name, DataPassadaPadrao, 10m, 1000m, NovaRefExterna()));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        if (instrumentoId is null)
        {
            Assert.Equal(PostgresErrorCodes.NotNullViolation, pgException.SqlState);
        }
        else
        {
            Assert.Equal("ck_movimentos_instrumento_id_nao_vazio", pgException.ConstraintName);
        }
    }

    [Fact]
    public async Task Movimentos_Insert_CompraComValorFinanceiroNegativo_EhRecusado()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, tipo: TipoMovimento.Compra.Name, qtdDelta: 10m, valorFinanceiro: -1m));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_valor_nao_negativo", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_CompraComValorFinanceiroZero_EhAceito()
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(connection, tipo: TipoMovimento.Compra.Name, qtdDelta: 10m, valorFinanceiro: 0m);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Movimentos_Insert_AjusteComValorFinanceiroNegativo_EhAceito()
    {
        using var connection = await OpenConnectionAsync();

        var clienteId = NovoClienteId();
        var instrumentoId = "td:tesouro-selic-2029";
        var original = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Compra.Name, qtdDelta: 10m, valorFinanceiro: 600m);
        var id = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -600m, refEstorno: original);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Movimentos_Insert_AjusteComValorFinanceiroPositivo_EhAceitoPorSerOEstornoDeUmAjusteNegativo()
    {
        using var connection = await OpenConnectionAsync();

        var clienteId = NovoClienteId();
        var instrumentoId = "td:tesouro-selic-2029";
        var original = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Compra.Name, qtdDelta: 10m, valorFinanceiro: 600m);
        var ajusteNegativo = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -600m, refEstorno: original);
        var ajustePositivo = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: 10m, valorFinanceiro: 600m, refEstorno: ajusteNegativo);
        Assert.True(ajustePositivo > 0);
    }

    [Fact]
    public async Task Movimentos_Insert_CupomComQtdDeltaDiferenteDeZero_EhRecusado()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, tipo: TipoMovimento.Cupom.Name, qtdDelta: 1m, valorFinanceiro: 100m));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_cupom_sem_quantidade", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_CupomComQtdDeltaZero_EhAceito()
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(connection, tipo: TipoMovimento.Cupom.Name, qtdDelta: 0m, valorFinanceiro: 100m);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Movimentos_Insert_OmitindoValorFinanceiro_EhRecusadoPelaColunaNotNull()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => connection.ExecuteAsync(
            """
            INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, qtd_delta, ref_externa)
            VALUES (@clienteId, @instrumentoId, @tipo, @dataEvento, @qtdDelta, @refExterna)
            """,
            new
            {
                clienteId = NovoClienteId(),
                instrumentoId = "td:tesouro-selic-2029",
                tipo = TipoMovimento.Compra.Name,
                dataEvento = DataPassadaPadrao,
                qtdDelta = 10m,
                refExterna = NovaRefExterna(),
            }));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.NotNullViolation, pgException.SqlState);
        Assert.Equal("valor_financeiro", pgException.ColumnName);
    }

    [Fact]
    public async Task Movimentos_Insert_OmitindoQtdDelta_EhRecusadoEmVezDeGravarZeroEmSilencio()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => connection.ExecuteAsync(
            """
            INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, valor_financeiro, ref_externa)
            VALUES (@clienteId, @instrumentoId, @tipo, @dataEvento, @valorFinanceiro, @refExterna)
            """,
            new
            {
                clienteId = NovoClienteId(),
                instrumentoId = "td:tesouro-selic-2029",
                tipo = TipoMovimento.Compra.Name,
                dataEvento = DataPassadaPadrao,
                valorFinanceiro = 100m,
                refExterna = NovaRefExterna(),
            }));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.NotNullViolation, pgException.SqlState);
        Assert.Equal("qtd_delta", pgException.ColumnName);
    }

    [Theory]
    [InlineData("movimentos", "qtd_delta", "quantidade")]
    [InlineData("movimentos", "valor_financeiro", "valor")]
    [InlineData("posicao_corrente", "quantidade", "quantidade")]
    [InlineData("posicao_corrente", "preco_medio", "preco")]
    [InlineData("posicao_corrente", "custo_total", "valor")]
    [InlineData("preco_atual", "valor", "preco")]
    [InlineData("historico_precos", "valor", "preco")]
    [InlineData("snapshots_posicao", "quantidade", "quantidade")]
    [InlineData("snapshots_posicao", "preco", "preco")]
    [InlineData("snapshots_posicao", "valor", "valor")]
    [InlineData("snapshots_posicao", "preco_medio", "preco")]
    [InlineData("snapshots_posicao", "custo", "valor")]
    public async Task ColunaNumeric_BateComANumericPrecisionEScaleDaConstanteDeSchemaNumericLimitsDaSuaFamilia(
        string tabela, string coluna, string familia)
    {
        using var connection = await OpenConnectionAsync();

        var colunas = await GetColumnsAsync(connection, tabela);
        var (precisaoEsperada, escalaEsperada) = familia switch
        {
            "quantidade" => (SchemaNumericLimits.QuantidadePrecisao, SchemaNumericLimits.QuantidadeEscala),
            "valor" => (SchemaNumericLimits.ValorPrecisao, SchemaNumericLimits.ValorEscala),
            "preco" => (SchemaNumericLimits.PrecoPrecisao, SchemaNumericLimits.PrecoEscala),
            _ => throw new ArgumentOutOfRangeException(nameof(familia), familia, "Família de constante numérica desconhecida."),
        };
        var info = colunas[coluna];
        Assert.Equal(precisaoEsperada, info.NumericPrecision);
        Assert.Equal(escalaEsperada, info.NumericScale);
    }

    [Fact]
    public async Task Movimentos_Insert_QtdDeltaComOnzeDigitosInteiros_EhRecusadoPorMagnitudeComErroNumericValueOutOfRange()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, qtdDelta: 12345678901.12345678m, valorFinanceiro: 100m));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.NumericValueOutOfRange, pgException.SqlState);
    }

    [Fact]
    public async Task Movimentos_Insert_QtdDeltaComEscalaNove_EhArredondadoEmSilencioParaEscalaOitoSemErroNenhum()
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(connection, qtdDelta: 1.123456789m, valorFinanceiro: 100m);
        var qtdDeltaGravado = await connection.ExecuteScalarAsync<decimal>(
            "SELECT qtd_delta FROM movimentos WHERE id = @id", new { id });
        Assert.Equal(1.12345679m, qtdDeltaGravado);
    }

    [Fact]
    public async Task NaoExisteIndiceForaDaConvencaoPkUxIxEmNenhumaDasCincoTabelasDoLivro()
    {
        using var connection = await OpenConnectionAsync();

        var nomes = await connection.QueryAsync<string>(
            """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename IN ('movimentos', 'posicao_corrente', 'preco_atual', 'historico_precos', 'snapshots_posicao')
            """);
        var lista = nomes.ToList();
        Assert.NotEmpty(lista);
        foreach (var nome in lista)
        {
            Assert.True(
                nome.StartsWith("PK_", StringComparison.Ordinal)
                || nome.StartsWith("ux_", StringComparison.Ordinal)
                || nome.StartsWith("ix_", StringComparison.Ordinal),
                $"Índice '{nome}' foge da convenção PK_/ux_/ix_ (PADROES §10.23) — provável índice gerado pelo EF sem HasDatabaseName explícito.");
        }
    }

    [Theory]
    [InlineData("clientes")]
    [InlineData("instrumentos")]
    [InlineData("instrumento_fontes")]
    [InlineData("cliente_instrumento")]
    [InlineData("de_para_instrumentos")]
    public async Task NaoExisteTabelaDeClientesInstrumentosOuDePara(string tabela)
    {
        using var connection = await OpenConnectionAsync();

        var existe = await TableExistsAsync(connection, tabela);
        Assert.False(existe, $"Tabela '{tabela}' não deveria existir (ADR-4/ADR-12: banco privado, sem de-para).");
    }

    [Fact]
    public async Task CkMovimentosTipoValido_ComoConjuntoBateExatamenteComTipoMovimentoAllEComDezValores()
    {
        using var connection = await OpenConnectionAsync();

        var definicao = await connection.ExecuteScalarAsync<string?>(
            """
            SELECT pg_get_constraintdef(con.oid)
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            WHERE c.relname = 'movimentos' AND con.conname = 'ck_movimentos_tipo_valido'
            """);
        Assert.True(definicao is not null, "ck_movimentos_tipo_valido: definição não encontrada.");
        var valoresNoCheck = Regex
            .Matches(definicao!, @"'([^']+)'::text")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
        Assert.NotEmpty(valoresNoCheck);
        var valoresEsperados = TipoMovimento.All.Select(t => t.Name).ToHashSet();
        Assert.Equal(10, valoresEsperados.Count);
        Assert.True(
            valoresEsperados.SetEquals(valoresNoCheck),
            "ck_movimentos_tipo_valido diverge de TipoMovimento.All — as duas cópias têm que andar " +
            $"juntas. No CHECK: [{string.Join(", ", valoresNoCheck)}], em TipoMovimento.All: " +
            $"[{string.Join(", ", valoresEsperados)}].");
    }

    [Theory]
    [InlineData("dividendo")]
    [InlineData("desdobramento")]
    [InlineData("grupamento")]
    [InlineData("bonificacao")]
    public async Task Movimentos_Insert_TipoDeFase2AindaNaoImplementado_EhRecusado(string tipo)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(connection, tipo: tipo));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_tipo_valido", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_ConjuntoCompletoDeUmResgate_GravaSeisLinhasSemColisaoDeRefExternaEComOsDoisInvariantesDePrecoDaV2()
    {
        using var connection = await OpenConnectionAsync();

        var tradeId = $"trade-schema-tests-{Guid.NewGuid():N}";
        var resgate = await InserirResgateCompletoAsync(connection, tradeId, quantidade: 10m, valorBruto: 1000m, ir: 150m, iof: 5m);

        Assert.Equal(6, resgate.Todas.Select(l => l.RefExterna).Distinct().Count());
        var contagem = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteId", new { clienteId = resgate.ClienteId });
        Assert.Equal(6, contagem);

        var somaALiquidarAntesDaLiquidacao = resgate.Ir.QtdDelta + resgate.Iof.QtdDelta + resgate.Aliq.QtdDelta;
        Assert.Equal(845m, somaALiquidarAntesDaLiquidacao);

        var somaALiquidarDepoisDaLiquidacao = somaALiquidarAntesDaLiquidacao + resgate.LiqAliq.QtdDelta;
        Assert.Equal(0m, somaALiquidarDepoisDaLiquidacao);

        var somaCaixaBrlDepoisDaLiquidacao = resgate.LiqBrl.QtdDelta;
        Assert.Equal(845m, somaCaixaBrlDepoisDaLiquidacao);

        var somaConjuntaAntesDasPernasDeLiquidacao = somaALiquidarAntesDaLiquidacao;
        var somaConjuntaDepoisDasPernasDeLiquidacao = somaALiquidarDepoisDaLiquidacao + somaCaixaBrlDepoisDaLiquidacao;
        Assert.Equal(somaConjuntaAntesDasPernasDeLiquidacao, somaConjuntaDepoisDasPernasDeLiquidacao);
    }

    [Fact]
    public async Task Movimentos_Insert_ConjuntoCompletoDeUmCupom_GravaCincoLinhasComAsChavesDaFamiliaDeCorpaction()
    {
        using var connection = await OpenConnectionAsync();

        var cupom = await InserirCupomCompletoAsync(connection, quantidade: 100m, valorPorUnidade: 2.5m, ir: 25m);

        Assert.Equal(5, cupom.Todas.Select(l => l.RefExterna).Distinct().Count());
        var contagem = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteId", new { clienteId = cupom.ClienteId });
        Assert.Equal(5, contagem);

        Assert.Equal(TipoMovimento.Cupom.Name, cupom.Principal.Tipo);
        Assert.Equal(cupom.InstrumentoId, cupom.Principal.InstrumentoId);
        Assert.Equal(0m, cupom.Principal.QtdDelta);
        Assert.Equal(250m, cupom.Principal.ValorFinanceiro);
    }

    [Fact]
    public async Task Movimentos_Insert_ConjuntoCompletoDeUmVencimento_GravaSeisLinhasComAsChavesDaFamiliaDeCorpaction()
    {
        using var connection = await OpenConnectionAsync();

        var vencimento = await InserirVencimentoCompletoAsync(connection, quantidade: 50m, precoFinal: 20m, ir: 100m, iof: 2m);

        Assert.Equal(6, vencimento.Todas.Select(l => l.RefExterna).Distinct().Count());
        var contagem = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteId", new { clienteId = vencimento.ClienteId });
        Assert.Equal(6, contagem);

        Assert.Equal(TipoMovimento.Resgate.Name, vencimento.Principal.Tipo);
        Assert.Equal(vencimento.InstrumentoId, vencimento.Principal.InstrumentoId);
        Assert.Equal(-50m, vencimento.Principal.QtdDelta);
        Assert.Equal(1000m, vencimento.Principal.ValorFinanceiro);
    }

    [Fact]
    public async Task Movimentos_Insert_ReversaoDeUmResgate_GravaSeisAjustesSimetricosQueSomamZeroEmQtdDeltaEValorFinanceiro()
    {
        using var connection = await OpenConnectionAsync();

        var tradeId = $"trade-schema-tests-{Guid.NewGuid():N}";
        var resgate = await InserirResgateCompletoAsync(connection, tradeId, quantidade: 10m, valorBruto: 1000m, ir: 150m, iof: 5m);

        var tradeIdEstorno = $"trade-estorno-schema-tests-{Guid.NewGuid():N}";
        var refsDeReversao = new Dictionary<string, string>
        {
            [resgate.Venda.RefExterna] = tradeIdEstorno,
            [resgate.Ir.RefExterna] = $"est:ir:{tradeIdEstorno}",
            [resgate.Iof.RefExterna] = $"est:iof:{tradeIdEstorno}",
            [resgate.Aliq.RefExterna] = $"est:aliq:{tradeIdEstorno}",
            [resgate.LiqAliq.RefExterna] = $"est:liq:{tradeIdEstorno}:aliq",
            [resgate.LiqBrl.RefExterna] = $"est:liq:{tradeIdEstorno}:brl",
        };

        await using var transacao = await connection.BeginTransactionAsync();
        var idsDosAjustes = new List<long>();
        foreach (var original in resgate.Todas)
        {
            var refExternaDoAjuste = refsDeReversao[original.RefExterna];
            var id = await InserirMovimentoBrutoAsync(
                connection, resgate.ClienteId, original.InstrumentoId, TipoMovimento.Ajuste.Name,
                original.DataEvento, -original.QtdDelta, -original.ValorFinanceiro, refExternaDoAjuste,
                refEstorno: original.Id, transaction: transacao);
            idsDosAjustes.Add(id);
        }
        await transacao.CommitAsync();

        var ajustesGravados = await LerLinhasGravadasPorIdAsync(connection, idsDosAjustes);

        foreach (var (original, idAjuste) in resgate.Todas.Zip(idsDosAjustes))
        {
            var ajuste = ajustesGravados[idAjuste];
            Assert.Equal(TipoMovimento.Ajuste.Name, ajuste.Tipo);
            Assert.Equal(original.InstrumentoId, ajuste.InstrumentoId);
            Assert.Equal(original.DataEvento, ajuste.DataEvento);
            Assert.Equal(0m, original.QtdDelta + ajuste.QtdDelta);
            Assert.Equal(0m, original.ValorFinanceiro + ajuste.ValorFinanceiro);
        }
    }

    [Fact]
    public async Task Movimentos_Insert_SegundoAjusteApontandoParaOMesmoMovimentoRevertido_EhRecusadoPeloIndiceUnicoParcial()
    {
        using var connection = await OpenConnectionAsync();

        var clienteId = NovoClienteId();
        var instrumentoId = "td:tesouro-selic-2029";
        var original = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Compra.Name, qtdDelta: 10m, valorFinanceiro: 1000m);
        await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -1000m, refEstorno: original);

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -1000m, refEstorno: original));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pgException.SqlState);
        Assert.Equal("ix_movimentos_ref_estorno_unico", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_AjusteApontandoParaAjusteAnterior_EhAceitoComoAExcecaoNomeadaDaSecao1021()
    {
        using var connection = await OpenConnectionAsync();

        var clienteId = NovoClienteId();
        var instrumentoId = "td:tesouro-selic-2029";
        var original = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Compra.Name, qtdDelta: 10m, valorFinanceiro: 1000m);
        var ajusteA = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: -10m, valorFinanceiro: -1000m, refEstorno: original);
        var ajusteB = await InserirMovimentoAsync(
            connection, clienteId: clienteId, instrumentoId: instrumentoId, tipo: TipoMovimento.Ajuste.Name,
            qtdDelta: 10m, valorFinanceiro: 1000m, refEstorno: ajusteA);

        var contagem = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM movimentos WHERE id = @ajusteB", new { ajusteB });
        Assert.Equal(1, contagem);
    }

    [Fact]
    public async Task Movimentos_IndiceRefEstornoUnico_EhUnicoEParcialSobreRefEstornoNaoNulo()
    {
        using var connection = await OpenConnectionAsync();

        var indexDef = await connection.ExecuteScalarAsync<string?>(
            """
            SELECT indexdef FROM pg_indexes
            WHERE schemaname = 'public' AND tablename = 'movimentos' AND indexname = 'ix_movimentos_ref_estorno_unico'
            """);
        Assert.True(indexDef is not null, "Índice 'ix_movimentos_ref_estorno_unico' não existe em movimentos.");
        Assert.Contains("UNIQUE", indexDef!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ref_estorno IS NOT NULL", indexDef, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("caixa:brl")]
    [InlineData("Caixa:BRL")]
    [InlineData("CAIXA:BRL")]
    [InlineData("caixa:")]
    [InlineData("caixa:USD")]
    public async Task Movimentos_Insert_InstrumentoIdDeCaixaForaDaAllowList_EhRecusadoPeloCheckDeCaixaValido(string instrumentoId)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, instrumentoId: instrumentoId, tipo: TipoMovimento.ALiquidar.Name, qtdDelta: 1m, valorFinanceiro: 1m));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_instrumento_caixa_valido", pgException.ConstraintName);
    }

    [Theory]
    [InlineData(" caixa:BRL")]
    [InlineData("  caixa:BRL")]
    [InlineData(" caixa:brl")]
    [InlineData("caixa:BRL ")]
    public async Task Movimentos_Insert_InstrumentoIdDeCaixaComEspacoNaBorda_EhRecusadoPeloCheckDeCaixaValido(string instrumentoId)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, instrumentoId: instrumentoId, tipo: TipoMovimento.ALiquidar.Name, qtdDelta: 1m, valorFinanceiro: 1m));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_instrumento_caixa_valido", pgException.ConstraintName);
    }

    [Theory]
    [InlineData(" td:x ")]
    [InlineData("td:x\t")]
    [InlineData("\ttd:x")]
    public async Task Movimentos_Insert_InstrumentoIdNaoCaixaComEspacoOuTabNaBorda_EhRecusadoPeloRegexDeBordaQueBtrimNaoPegaria(string instrumentoId)
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, instrumentoId: instrumentoId, tipo: TipoMovimento.Compra.Name, qtdDelta: 1m, valorFinanceiro: 1m));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_instrumento_id_sem_espaco_nas_bordas", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_ClienteIdComEspacoNaBorda_EhRecusadoPeloRegexDeBordaMostrandoQueAClasseEDosTresIdentificadores()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, clienteId: $" cli-1-{Guid.NewGuid():N}"));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_cliente_id_sem_espaco_nas_bordas", pgException.ConstraintName);
    }

    [Fact]
    public async Task Movimentos_Insert_RefExternaComEspacoNaBorda_EhRecusadoPeloRegexDeBordaMostrandoQueAClasseEDosTresIdentificadores()
    {
        using var connection = await OpenConnectionAsync();

        var exception = await Record.ExceptionAsync(() => InserirMovimentoAsync(
            connection, refExterna: $"k-{Guid.NewGuid():N} "));
        Assert.NotNull(exception);
        var pgException = Assert.IsType<PostgresException>(exception);
        Assert.Equal("ck_movimentos_ref_externa_sem_espaco_nas_bordas", pgException.ConstraintName);
    }

    [Theory]
    [InlineData("caixa:BRL")]
    [InlineData("caixa:a_liquidar")]
    [InlineData("td:tesouro-selic-2029")]
    public async Task Movimentos_Insert_InstrumentoIdValidoPelaAllowListDeCaixaOuNaoCaixa_EhAceito(string instrumentoId)
    {
        using var connection = await OpenConnectionAsync();

        var id = await InserirMovimentoAsync(
            connection, instrumentoId: instrumentoId, tipo: TipoMovimento.ALiquidar.Name, qtdDelta: 1m, valorFinanceiro: 1m);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Movimentos_Insert_AporteNoInstrumentoDoTituloComQtdDeltaPositiva_EhAceito()
    {
        using var connection = await OpenConnectionAsync();

        var instrumentoId = "td:tesouro-selic-2029";
        var id = await InserirMovimentoAsync(
            connection, instrumentoId: instrumentoId, tipo: TipoMovimento.Aporte.Name, qtdDelta: 5m, valorFinanceiro: 500m);

        var linha = await connection.QuerySingleAsync<(string InstrumentoId, decimal QtdDelta)>(
            "SELECT instrumento_id AS \"InstrumentoId\", qtd_delta AS \"QtdDelta\" FROM movimentos WHERE id = @id", new { id });
        Assert.Equal(instrumentoId, linha.InstrumentoId);
        Assert.True(linha.QtdDelta > 0);
    }

    [Fact]
    public async Task Movimentos_Insert_DataEventoDeAmanhaEmSaoPaulo_EhRecusadoEAMesmaExpressaoSemMaisUmDia_EhAceita()
    {
        using var connection = await OpenConnectionAsync();

        var clienteIdRecusado = NovoClienteId();
        var exceptionAmanha = await Record.ExceptionAsync(() => connection.ExecuteAsync(
            """
            INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, qtd_delta, valor_financeiro, ref_externa)
            VALUES (@clienteId, 'td:tesouro-selic-2029', 'compra', (now() AT TIME ZONE 'America/Sao_Paulo')::date + 1, 10, 1000, @refExterna)
            """,
            new { clienteId = clienteIdRecusado, refExterna = NovaRefExterna() }));
        Assert.NotNull(exceptionAmanha);
        Assert.IsType<PostgresException>(exceptionAmanha);

        var clienteIdAceito = NovoClienteId();
        var refExternaAceita = NovaRefExterna();
        await connection.ExecuteAsync(
            """
            INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, qtd_delta, valor_financeiro, ref_externa)
            VALUES (@clienteId, 'td:tesouro-selic-2029', 'compra', (now() AT TIME ZONE 'America/Sao_Paulo')::date, 10, 1000, @refExterna)
            """,
            new { clienteId = clienteIdAceito, refExterna = refExternaAceita });
        var contagem = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteIdAceito", new { clienteIdAceito });
        Assert.Equal(1, contagem);
    }

    [Fact]
    public async Task Movimentos_TriggerDeDataFutura_DiscriminaAtTimeZoneSaoPauloDeCurrentDateEmAoMenosUmaDasDuasJanelasHorariasQueJuntasCobremODia()
    {
        using var referencia = await OpenConnectionAsync();
        var brt = await ObterDataDeHojeEmSaoPauloAsync(referencia);

        var exercitouAlgumaDirecao = false;

        await using (var sessaoUmDiaAFrenteDoBrt = await OpenConnectionAsync())
        {
            await sessaoUmDiaAFrenteDoBrt.ExecuteAsync("SET TIME ZONE 'Pacific/Kiritimati'");
            var currentDateNaSessao = await sessaoUmDiaAFrenteDoBrt.ExecuteScalarAsync<DateOnly>("SELECT current_date");
            if (currentDateNaSessao > brt)
            {
                exercitouAlgumaDirecao = true;
                var exception = await Record.ExceptionAsync(() => sessaoUmDiaAFrenteDoBrt.ExecuteAsync(
                    """
                    INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, qtd_delta, valor_financeiro, ref_externa)
                    VALUES (@clienteId, 'td:tesouro-selic-2029', 'compra', current_date, 10, 1000, @refExterna)
                    """,
                    new { clienteId = NovoClienteId(), refExterna = NovaRefExterna() }));
                Assert.NotNull(exception);
                Assert.IsType<PostgresException>(exception);
            }
        }

        await using (var sessaoUmDiaAtrasDoBrt = await OpenConnectionAsync())
        {
            await sessaoUmDiaAtrasDoBrt.ExecuteAsync("SET TIME ZONE 'Etc/GMT+12'");
            var currentDateNaSessao = await sessaoUmDiaAtrasDoBrt.ExecuteScalarAsync<DateOnly>("SELECT current_date");
            if (currentDateNaSessao < brt)
            {
                exercitouAlgumaDirecao = true;
                var clienteId = NovoClienteId();
                await sessaoUmDiaAtrasDoBrt.ExecuteAsync(
                    """
                    INSERT INTO movimentos (cliente_id, instrumento_id, tipo, data_evento, qtd_delta, valor_financeiro, ref_externa)
                    VALUES (@clienteId, 'td:tesouro-selic-2029', 'compra', (now() AT TIME ZONE 'America/Sao_Paulo')::date, 10, 1000, @refExterna)
                    """,
                    new { clienteId, refExterna = NovaRefExterna() });
                var contagem = await sessaoUmDiaAtrasDoBrt.ExecuteScalarAsync<long>(
                    "SELECT COUNT(*) FROM movimentos WHERE cliente_id = @clienteId", new { clienteId });
                Assert.Equal(1, contagem);
            }
        }

        Assert.True(
            exercitouAlgumaDirecao,
            "Nenhuma das duas janelas horárias valeu (07:00-24:00 BRT para a sessão à frente, " +
            "00:00-09:00 BRT para a sessão atrás) — o teste não discriminou a implementação nesta hora.");
    }
}
