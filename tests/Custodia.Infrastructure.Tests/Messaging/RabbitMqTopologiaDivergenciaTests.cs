using Custodia.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;

namespace Custodia.Infrastructure.Tests.Messaging;

public sealed class RabbitMqTopologiaDivergenciaTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    public Task InitializeAsync() => _rabbitMq.StartAsync();

    public Task DisposeAsync() => _rabbitMq.DisposeAsync().AsTask();

    private RabbitMqConnectionProvider CriarConnectionProvider()
    {
        var uri = new Uri(_rabbitMq.GetConnectionString());
        var credenciais = uri.UserInfo.Split(':', 2);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = uri.Host,
                ["RabbitMq:Port"] = uri.Port.ToString(),
                ["RabbitMq:User"] = credenciais[0],
                ["RabbitMq:Password"] = credenciais[1],
                ["RabbitMq:VirtualHost"] = "/",
            })
            .Build();

        return new RabbitMqConnectionProvider(configuration, NullLogger<RabbitMqConnectionProvider>.Instance);
    }

    [Fact]
    public async Task ObterConexaoAsync_PrimeiraVez_DeclaraATopologiaInteiraSemLancar()
    {
        await using var connectionProvider = CriarConnectionProvider();

        var excecao = await Record.ExceptionAsync(() => connectionProvider.ObterConexaoAsync(CancellationToken.None));

        Assert.Null(excecao);
    }

    [Fact]
    public async Task RedeclaracaoComArgumentosDivergentes_DevolveExcecao406_NaoEEngolida()
    {
        await using var connectionProviderInicial = CriarConnectionProvider();
        await connectionProviderInicial.ObterConexaoAsync(CancellationToken.None);

        var uri = new Uri(_rabbitMq.GetConnectionString());
        var credenciais = uri.UserInfo.Split(':', 2);
        var factory = new ConnectionFactory
        {
            HostName = uri.Host,
            Port = uri.Port,
            UserName = credenciais[0],
            Password = credenciais[1],
        };

        await using var conexaoAdmin = await factory.CreateConnectionAsync();
        await using (var canalAdmin = await conexaoAdmin.CreateChannelAsync())
        {
            await canalAdmin.QueueDeleteAsync(RabbitMqTopologia.FilaPrincipal);
            await canalAdmin.QueueDeclareAsync(
                RabbitMqTopologia.FilaPrincipal,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-queue-type"] = "classic" });
        }

        await using var connectionProviderDivergente = CriarConnectionProvider();

        var excecao = await Record.ExceptionAsync(
            () => connectionProviderDivergente.ObterConexaoAsync(CancellationToken.None));

        Assert.NotNull(excecao);
        var operacaoInterrompida = Assert.IsAssignableFrom<OperationInterruptedException>(excecao);
        Assert.Equal((ushort)406, operacaoInterrompida.ShutdownReason?.ReplyCode);
    }
}
