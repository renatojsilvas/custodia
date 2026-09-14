using System.Globalization;
using Custodia.Application;
using Custodia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Custodia.Infrastructure.Tests.Messaging;

public sealed class RabbitMqConsumidorFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    public string ConnectionStringPostgres { get; private set; } = string.Empty;

    public string RabbitMqHost { get; private set; } = string.Empty;

    public int RabbitMqPort { get; private set; }

    public string RabbitMqUser { get; private set; } = string.Empty;

    public string RabbitMqPassword { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        ConnectionStringPostgres = _postgres.GetConnectionString();

        var uri = new Uri(_rabbitMq.GetConnectionString());
        var credenciais = uri.UserInfo.Split(':', 2);
        RabbitMqHost = uri.Host;
        RabbitMqPort = uri.Port;
        RabbitMqUser = credenciais[0];
        RabbitMqPassword = credenciais[1];

        await using var db = CriarDbContext();
        await db.Database.MigrateAsync();

        await using var conexao = await CriarConexaoAmqpAsync();
        await using var canal = await conexao.CreateChannelAsync();
        await Custodia.Infrastructure.Messaging.RabbitMqTopologia.DeclararAsync(canal, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }

    public AppDbContext CriarDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionStringPostgres).Options;
        return new AppDbContext(options);
    }

    public IConfiguration CriarConfiguration(IDictionary<string, string?>? extras = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionStringPostgres,
            ["RabbitMq:Host"] = RabbitMqHost,
            ["RabbitMq:Port"] = RabbitMqPort.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:User"] = RabbitMqUser,
            ["RabbitMq:Password"] = RabbitMqPassword,
            ["RabbitMq:VirtualHost"] = "/",
        };

        if (extras is not null)
        {
            foreach (var par in extras)
            {
                valores[par.Key] = par.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    public ServiceProvider CriarServiceProvider(
        IConfiguration configuration, Action<IServiceCollection>? configurarServicosExtras = null)
    {
        var servicos = new ServiceCollection();
        servicos.AddSingleton(configuration);
        servicos.AddLogging(b =>
        {
            b.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            b.AddProvider(new DebugConsoleLoggerProvider());
        });
        servicos.AddApplication();
        servicos.AddInfrastructure(configuration);
        configurarServicosExtras?.Invoke(servicos);
        return servicos.BuildServiceProvider();
    }

    public Task<IConnection> CriarConexaoAmqpAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = RabbitMqHost,
            Port = RabbitMqPort,
            UserName = RabbitMqUser,
            Password = RabbitMqPassword,
        };

        return factory.CreateConnectionAsync();
    }

    public async Task LimparEstadoAsync()
    {
        await using var db = CriarDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE movimentos, posicao_corrente, preco_atual, historico_precos RESTART IDENTITY;");

        await using var conexao = await CriarConexaoAmqpAsync();
        await using var canal = await conexao.CreateChannelAsync();

        foreach (var fila in new[]
                 {
                     Custodia.Infrastructure.Messaging.RabbitMqTopologia.FilaPrincipal,
                     Custodia.Infrastructure.Messaging.RabbitMqTopologia.FilaDlq,
                     Custodia.Infrastructure.Messaging.RabbitMqTopologia.FilaRetry,
                     Custodia.Infrastructure.Messaging.RabbitMqTopologia.FilaParked,
                 })
        {
            await canal.QueuePurgeAsync(fila);
        }
    }
}

[CollectionDefinition("rabbitmq-consumidor")]
public sealed class RabbitMqConsumidorCollection : ICollectionFixture<RabbitMqConsumidorFixture>;

internal sealed class DebugConsoleLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
{
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new DebugConsoleLogger(categoryName);
    public void Dispose() { }

    private sealed class DebugConsoleLogger(string categoryName) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
            if (exception is not null) Console.WriteLine(exception);
        }
    }
}
