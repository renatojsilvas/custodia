using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Custodia.Infrastructure.Persistence.Migrations
{
    public partial class CriaCalendarioDiasUteis : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendario_dias_uteis",
                columns: table => new
                {
                    data = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendario_dias_uteis", x => x.data);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO calendario_dias_uteis (data)
                SELECT dia::date
                FROM generate_series('2024-01-01'::date, '2030-12-31'::date, interval '1 day') AS dia
                WHERE EXTRACT(ISODOW FROM dia) < 6
                  AND dia::date <> ALL (ARRAY[
                    '2024-01-01','2024-02-12','2024-02-13','2024-03-29','2024-04-21','2024-05-01','2024-05-30','2024-09-07','2024-10-12','2024-11-02','2024-11-15','2024-11-20','2024-12-25',
                    '2025-01-01','2025-03-03','2025-03-04','2025-04-18','2025-04-21','2025-05-01','2025-06-19','2025-09-07','2025-10-12','2025-11-02','2025-11-15','2025-11-20','2025-12-25',
                    '2026-01-01','2026-02-16','2026-02-17','2026-04-03','2026-04-21','2026-05-01','2026-06-04','2026-09-07','2026-10-12','2026-11-02','2026-11-15','2026-11-20','2026-12-25',
                    '2027-01-01','2027-02-08','2027-02-09','2027-03-26','2027-04-21','2027-05-01','2027-05-27','2027-09-07','2027-10-12','2027-11-02','2027-11-15','2027-11-20','2027-12-25',
                    '2028-01-01','2028-02-28','2028-02-29','2028-04-14','2028-04-21','2028-05-01','2028-06-15','2028-09-07','2028-10-12','2028-11-02','2028-11-15','2028-11-20','2028-12-25',
                    '2029-01-01','2029-02-12','2029-02-13','2029-03-30','2029-04-21','2029-05-01','2029-05-31','2029-09-07','2029-10-12','2029-11-02','2029-11-15','2029-11-20','2029-12-25',
                    '2030-01-01','2030-03-04','2030-03-05','2030-04-19','2030-04-21','2030-05-01','2030-06-20','2030-09-07','2030-10-12','2030-11-02','2030-11-15','2030-11-20','2030-12-25'
                  ]::date[]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendario_dias_uteis");
        }
    }
}
