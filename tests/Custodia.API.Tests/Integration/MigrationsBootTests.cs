using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Custodia.API.Tests.Integration;

public sealed class MigrationsBootTests
{
    private const string MigrationId = "20260907234841_InitialCreate";
    private const string ConnectionStringEnvVar = "ConnectionStrings__DefaultConnection";
    private const string RabbitMqHostEnvVar = "RabbitMq__Host";
    private const string RabbitMqUserEnvVar = "RabbitMq__User";
    private const string RabbitMqPasswordEnvVar = "RabbitMq__Password";

    [Fact]
    public async Task Boot_ComPostgresReal_AplicaMigrationsERegistraALinhaEmEfMigrationsHistory()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, connectionString);
        DefinirVariaveisDoRabbitMq();
        try
        {
            await using var factory = new DevelopmentBootFactory();

            _ = factory.Services;
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
            LimparVariaveisDoRabbitMq();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = @migrationId
            """;
        command.Parameters.AddWithValue("migrationId", MigrationId);

        var count = (long)(await command.ExecuteScalarAsync())!;

        Assert.True(
            count == 1,
            $"esperava exatamente 1 linha para a migration '{MigrationId}' em __EFMigrationsHistory " +
            $"após o boot (aplicada automaticamente pelo DatabaseInitializer), encontrado count={count}. " +
            "Isso prova migrations-no-boot (o coração do F1), não uma chamada manual a MigrateAsync.");
    }

    [Fact]
    public async Task Boot_ComPostgresReal_HealthReadyRespondeOkAposMigrar()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, postgres.GetConnectionString());
        DefinirVariaveisDoRabbitMq();
        try
        {
            await using var factory = new DevelopmentBootFactory();
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/health/ready", CancellationToken.None);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
            LimparVariaveisDoRabbitMq();
        }
    }

    private static void DefinirVariaveisDoRabbitMq()
    {
        Environment.SetEnvironmentVariable(RabbitMqHostEnvVar, "127.0.0.1");
        Environment.SetEnvironmentVariable(RabbitMqUserEnvVar, "guest");
        Environment.SetEnvironmentVariable(RabbitMqPasswordEnvVar, "guest");
    }

    private static void LimparVariaveisDoRabbitMq()
    {
        Environment.SetEnvironmentVariable(RabbitMqHostEnvVar, null);
        Environment.SetEnvironmentVariable(RabbitMqUserEnvVar, null);
        Environment.SetEnvironmentVariable(RabbitMqPasswordEnvVar, null);
    }

    private sealed class DevelopmentBootFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
