using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Custodia.Infrastructure.Persistence.Migrations
{
    public partial class AdicionaErrcodeAosTriggersDeMovimentos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION movimentos_bloqueia_update_delete() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION
                        'movimentos é append-only: % não é permitido (id=%). Correção é por INSERT de um movimento de estorno.',
                        TG_OP, COALESCE(OLD.id::text, 'desconhecido')
                        USING ERRCODE = 'MV002';
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION movimentos_bloqueia_data_futura() RETURNS trigger AS $$
                BEGIN
                    IF NEW.data_evento > (now() AT TIME ZONE 'America/Sao_Paulo')::date THEN
                        RAISE EXCEPTION
                            'movimentos não aceita data_evento futura: % informado, % é hoje em America/Sao_Paulo.',
                            NEW.data_evento, (now() AT TIME ZONE 'America/Sao_Paulo')::date
                            USING ERRCODE = 'MV001';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION movimentos_bloqueia_update_delete() RETURNS trigger AS $$
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
                CREATE OR REPLACE FUNCTION movimentos_bloqueia_data_futura() RETURNS trigger AS $$
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
        }
    }
}
