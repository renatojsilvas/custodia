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

    private const string RecriaPrecoAtualSql =
        """
        CREATE TABLE preco_atual (
            instrumento_id text NOT NULL,
            data_ref date NOT NULL,
            campo text NOT NULL,
            valor numeric(18,6) NOT NULL,
            revisao integer NOT NULL,
            CONSTRAINT "PK_preco_atual" PRIMARY KEY (instrumento_id)
        );
        """;

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
        await ExecuteSqlAsync("DROP TABLE preco_atual CASCADE;");
        try
        {
            var driftResponse = await _client.GetAsync("/health/ready", CancellationToken.None);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, driftResponse.StatusCode);

            var liveDuranteDrift = await _client.GetAsync("/health/live", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, liveDuranteDrift.StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync(RecriaPrecoAtualSql);
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
