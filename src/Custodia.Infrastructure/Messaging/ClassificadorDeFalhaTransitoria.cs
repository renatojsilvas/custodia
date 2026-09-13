using Npgsql;
using RabbitMQ.Client.Exceptions;

namespace Custodia.Infrastructure.Messaging;

public static class ClassificadorDeFalhaTransitoria
{
    public static bool EhTransitoria(Exception excecao) => excecao switch
    {
        NpgsqlException npgsql => npgsql.IsTransient,
        BrokerUnreachableException => true,
        TimeoutException => true,
        _ => false,
    };
}
