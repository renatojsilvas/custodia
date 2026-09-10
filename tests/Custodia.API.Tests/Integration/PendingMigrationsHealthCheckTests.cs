using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Custodia.Infrastructure.Persistence;

namespace Custodia.API.Tests.Integration;

[Collection("api")]
public sealed class PendingMigrationsHealthCheckTests(ApiTestFactory factory)
{
    private const string InitialCreateMigrationId = "20260907234841_InitialCreate";

    private const string OcultaPrecoAtualSql =
        "ALTER TABLE IF EXISTS preco_atual RENAME TO preco_atual_ausente_para_teste;";

    private const string RestauraPrecoAtualSql =
        "ALTER TABLE IF EXISTS preco_atual_ausente_para_teste RENAME TO preco_atual;";

    private const string RecriaTriggerImutavelSql =
        """
        CREATE TRIGGER trg_movimentos_imutavel
        BEFORE UPDATE OR DELETE ON movimentos
        FOR EACH ROW EXECUTE FUNCTION movimentos_bloqueia_update_delete();
        """;

    private const string RecriaTriggerDataEventoFuturaSql =
        """
        CREATE TRIGGER trg_movimentos_data_evento_futura
        BEFORE INSERT ON movimentos
        FOR EACH ROW EXECUTE FUNCTION movimentos_bloqueia_data_futura();
        """;

    private const string OcultaIndiceRefEstornoUnicoSql =
        "ALTER INDEX ix_movimentos_ref_estorno_unico RENAME TO ix_movimentos_ref_estorno_unico_oculto;";

    private const string RestauraIndiceRefEstornoUnicoSql =
        "ALTER INDEX ix_movimentos_ref_estorno_unico_oculto RENAME TO ix_movimentos_ref_estorno_unico;";

    private const string OcultaForeignKeyRefEstornoSql =
        """
        ALTER TABLE movimentos RENAME CONSTRAINT "FK_movimentos_movimentos_ref_estorno"
        TO "FK_movimentos_movimentos_ref_estorno_oculto";
        """;

    private const string RestauraForeignKeyRefEstornoSql =
        """
        ALTER TABLE movimentos RENAME CONSTRAINT "FK_movimentos_movimentos_ref_estorno_oculto"
        TO "FK_movimentos_movimentos_ref_estorno";
        """;

    private const string OcultaChaveAlternadaSql =
        "ALTER TABLE movimentos RENAME CONSTRAINT ux_movimentos_id_cliente_instrumento " +
        "TO ux_movimentos_id_cliente_instrumento_oculto;";

    private const string RestauraChaveAlternadaSql =
        "ALTER TABLE movimentos RENAME CONSTRAINT ux_movimentos_id_cliente_instrumento_oculto " +
        "TO ux_movimentos_id_cliente_instrumento;";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HealthReady_ComSchemaIntegro_RespondeOk()
    {
        var response = await _client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComTabelaDropadaPorFora_RespondeUnhealthy_ERecriarRestauraHealthy()
    {
        await ExecuteSqlAsync(OcultaPrecoAtualSql);
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RestauraPrecoAtualSql);
        }

        var restauradoResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, restauradoResponse.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComTriggerDeImutabilidadeDropadaPorFora_RespondeUnhealthy_ERecriarRestauraHealthy()
    {
        await ExecuteSqlAsync("DROP TRIGGER trg_movimentos_imutavel ON movimentos;");
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RecriaTriggerImutavelSql);
        }

        var restauradoResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, restauradoResponse.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComTriggerDeDataEventoFuturaDropadaPorFora_RespondeUnhealthy_ERecriarRestauraHealthy()
    {
        await ExecuteSqlAsync("DROP TRIGGER trg_movimentos_data_evento_futura ON movimentos;");
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RecriaTriggerDataEventoFuturaSql);
        }

        var restauradoResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, restauradoResponse.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComIndiceOcultoPorFora_RespondeUnhealthy_ERestaurarDevolveHealthy()
    {
        await ExecuteSqlAsync(OcultaIndiceRefEstornoUnicoSql);
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RestauraIndiceRefEstornoUnicoSql);
        }

        var restauradoResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, restauradoResponse.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComForeignKeyOcultaPorFora_RespondeUnhealthy_ERestaurarDevolveHealthy()
    {
        await ExecuteSqlAsync(OcultaForeignKeyRefEstornoSql);
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RestauraForeignKeyRefEstornoSql);
        }

        var restauradoResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, restauradoResponse.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComChaveAlternadaOcultaPorFora_RespondeUnhealthy_ERestaurarDevolveHealthy()
    {
        await ExecuteSqlAsync(OcultaChaveAlternadaSql);
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RestauraChaveAlternadaSql);
        }

        var restauradoResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, restauradoResponse.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ComMigrationPendente_RespondeUnhealthy_EDepoisDeAplicarRespondeHealthy()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrator = db.GetService<IMigrator>();

            await migrator.MigrateAsync(InitialCreateMigrationId);
        }

        try
        {
            var pendingResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, pendingResponse.StatusCode);

            var liveDurantePendente = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDurantePendente.StatusCode);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        var okResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
