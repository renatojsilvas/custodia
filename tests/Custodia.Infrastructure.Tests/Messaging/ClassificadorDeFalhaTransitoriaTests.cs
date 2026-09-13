using System.Net.Sockets;
using Custodia.Infrastructure.Messaging;
using Npgsql;
using RabbitMQ.Client.Exceptions;

namespace Custodia.Infrastructure.Tests.Messaging;

public sealed class ClassificadorDeFalhaTransitoriaTests
{
    [Fact]
    public void EhTransitoria_NpgsqlExceptionEnvolvendoFalhaDeConexao_DevolveVerdadeiro()
    {
        var excecao = new NpgsqlException("falha ao conectar", new SocketException());

        Assert.True(ClassificadorDeFalhaTransitoria.EhTransitoria(excecao));
    }

    [Fact]
    public void EhTransitoria_PostgresExceptionDeViolacaoDeUnicidade_DevolveFalso()
    {
        var excecao = new PostgresException("chave duplicada", "ERROR", "ERROR", "23505");

        Assert.False(ClassificadorDeFalhaTransitoria.EhTransitoria(excecao));
    }

    [Fact]
    public void EhTransitoria_PostgresExceptionDeSerializationFailure_DevolveVerdadeiro()
    {
        var excecao = new PostgresException("serialization failure", "ERROR", "ERROR", "40001");

        Assert.True(ClassificadorDeFalhaTransitoria.EhTransitoria(excecao));
    }

    [Fact]
    public void EhTransitoria_TimeoutException_DevolveVerdadeiro()
    {
        Assert.True(ClassificadorDeFalhaTransitoria.EhTransitoria(new TimeoutException()));
    }

    [Fact]
    public void EhTransitoria_BrokerUnreachableException_DevolveVerdadeiro()
    {
        var excecao = new BrokerUnreachableException(new SocketException());

        Assert.True(ClassificadorDeFalhaTransitoria.EhTransitoria(excecao));
    }

    [Fact]
    public void EhTransitoria_ExcecaoDeterministicaNaoClassificada_DevolveFalso()
    {
        Assert.False(ClassificadorDeFalhaTransitoria.EhTransitoria(new InvalidOperationException("bug determinístico")));
    }
}
