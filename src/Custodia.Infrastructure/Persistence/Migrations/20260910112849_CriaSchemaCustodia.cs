using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Custodia.Infrastructure.Persistence.Migrations
{
    public partial class CriaSchemaCustodia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historico_precos",
                columns: table => new
                {
                    instrumento_id = table.Column<string>(type: "text", nullable: false),
                    data_ref = table.Column<DateOnly>(type: "date", nullable: false),
                    campo = table.Column<string>(type: "text", nullable: false),
                    fonte = table.Column<string>(type: "text", nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    valor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    observado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_precos", x => new { x.instrumento_id, x.data_ref, x.campo, x.fonte, x.revisao });
                });

            migrationBuilder.CreateTable(
                name: "movimentos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cliente_id = table.Column<string>(type: "text", nullable: false),
                    instrumento_id = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    data_evento = table.Column<DateOnly>(type: "date", nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    qtd_delta = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    valor_financeiro = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ref_externa = table.Column<string>(type: "text", nullable: false),
                    ref_estorno = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimentos", x => x.id);
                    table.UniqueConstraint("ux_movimentos_id_cliente_instrumento", x => new { x.id, x.cliente_id, x.instrumento_id });
                    table.CheckConstraint("ck_movimentos_ajuste_coerente", "(tipo = 'ajuste') = (ref_estorno IS NOT NULL)");
                    table.CheckConstraint("ck_movimentos_cliente_id_nao_vazio", "btrim(cliente_id) <> ''");
                    table.CheckConstraint("ck_movimentos_cliente_id_sem_espaco_nas_bordas", "cliente_id !~ '^[\\s\\u00A0\\u1680\\u2007\\u202F]|[\\s\\u00A0\\u1680\\u2007\\u202F]$'");
                    table.CheckConstraint("ck_movimentos_cupom_sem_quantidade", "tipo <> 'cupom' OR qtd_delta = 0");
                    table.CheckConstraint("ck_movimentos_estorno_nao_auto", "ref_estorno IS NULL OR ref_estorno <> id");
                    table.CheckConstraint("ck_movimentos_instrumento_caixa_valido", "lower(btrim(instrumento_id)) NOT LIKE 'caixa:%' OR instrumento_id IN ('caixa:BRL', 'caixa:a_liquidar')");
                    table.CheckConstraint("ck_movimentos_instrumento_id_nao_vazio", "btrim(instrumento_id) <> ''");
                    table.CheckConstraint("ck_movimentos_instrumento_id_sem_espaco_nas_bordas", "instrumento_id !~ '^[\\s\\u00A0\\u1680\\u2007\\u202F]|[\\s\\u00A0\\u1680\\u2007\\u202F]$'");
                    table.CheckConstraint("ck_movimentos_ref_externa_nao_vazia", "btrim(ref_externa) <> ''");
                    table.CheckConstraint("ck_movimentos_ref_externa_sem_espaco_nas_bordas", "ref_externa !~ '^[\\s\\u00A0\\u1680\\u2007\\u202F]|[\\s\\u00A0\\u1680\\u2007\\u202F]$'");
                    table.CheckConstraint("ck_movimentos_tipo_valido", "tipo IN ('compra', 'venda', 'aporte', 'cupom', 'resgate', 'ir_retido', 'iof', 'a_liquidar', 'liquidacao', 'ajuste')");
                    table.CheckConstraint("ck_movimentos_valor_nao_negativo", "tipo = 'ajuste' OR valor_financeiro >= 0");
                    table.ForeignKey(
                        name: "FK_movimentos_movimentos_ref_estorno",
                        columns: x => new { x.ref_estorno, x.cliente_id, x.instrumento_id },
                        principalTable: "movimentos",
                        principalColumns: new[] { "id", "cliente_id", "instrumento_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "posicao_corrente",
                columns: table => new
                {
                    cliente_id = table.Column<string>(type: "text", nullable: false),
                    instrumento_id = table.Column<string>(type: "text", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    preco_medio = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    custo_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posicao_corrente", x => new { x.cliente_id, x.instrumento_id });
                });

            migrationBuilder.CreateTable(
                name: "preco_atual",
                columns: table => new
                {
                    instrumento_id = table.Column<string>(type: "text", nullable: false),
                    data_ref = table.Column<DateOnly>(type: "date", nullable: false),
                    campo = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    revisao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preco_atual", x => x.instrumento_id);
                });

            migrationBuilder.CreateTable(
                name: "snapshots_posicao",
                columns: table => new
                {
                    cliente_id = table.Column<string>(type: "text", nullable: false),
                    instrumento_id = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    calculado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    quantidade = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    preco = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    preco_medio = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    custo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshots_posicao", x => new { x.cliente_id, x.instrumento_id, x.data, x.calculado_em });
                });

            migrationBuilder.CreateIndex(
                name: "ix_historico_precos_lookup",
                table: "historico_precos",
                columns: new[] { "instrumento_id", "data_ref" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_cliente_instrumento_data_evento_registrado_em_id",
                table: "movimentos",
                columns: new[] { "cliente_id", "instrumento_id", "data_evento", "registrado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_cliente_ref_externa",
                table: "movimentos",
                columns: new[] { "cliente_id", "ref_externa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_ref_estorno_cliente_instrumento",
                table: "movimentos",
                columns: new[] { "ref_estorno", "cliente_id", "instrumento_id" });

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_ref_estorno_unico",
                table: "movimentos",
                column: "ref_estorno",
                unique: true,
                filter: "ref_estorno IS NOT NULL");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION movimentos_bloqueia_update_delete() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION
                        'movimentos é append-only: % não é permitido (id=%). Correção é por INSERT de um movimento de estorno.',
                        TG_OP, COALESCE(OLD.id::text, 'desconhecido');
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_movimentos_imutavel
                BEFORE UPDATE OR DELETE ON movimentos
                FOR EACH ROW EXECUTE FUNCTION movimentos_bloqueia_update_delete();
                """);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION movimentos_bloqueia_data_futura() RETURNS trigger AS $$
                BEGIN
                    IF NEW.data_evento > (now() AT TIME ZONE 'America/Sao_Paulo')::date THEN
                        RAISE EXCEPTION
                            'movimentos não aceita data_evento futura: % informado, % é hoje em America/Sao_Paulo.',
                            NEW.data_evento, (now() AT TIME ZONE 'America/Sao_Paulo')::date;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_movimentos_data_evento_futura
                BEFORE INSERT ON movimentos
                FOR EACH ROW EXECUTE FUNCTION movimentos_bloqueia_data_futura();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_movimentos_data_evento_futura ON movimentos;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS movimentos_bloqueia_data_futura();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_movimentos_imutavel ON movimentos;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS movimentos_bloqueia_update_delete();");

            migrationBuilder.DropTable(
                name: "historico_precos");

            migrationBuilder.DropTable(
                name: "movimentos");

            migrationBuilder.DropTable(
                name: "posicao_corrente");

            migrationBuilder.DropTable(
                name: "preco_atual");

            migrationBuilder.DropTable(
                name: "snapshots_posicao");
        }
    }
}
