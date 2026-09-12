using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Custodia.API.Extensions;

public static class RabbitMqConfigGuard
{
    private const string HostKey = "RabbitMq:Host";
    private const string UserKey = "RabbitMq:User";
    private const string PasswordKey = "RabbitMq:Password";

    public static void Validate(string environmentName, string? host, string? user, string? password)
    {
        if (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException(
                $"Configuração inválida: '{HostKey}' está ausente ou vazia em ambiente '{environmentName}'. {Hint}");
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException(
                $"Configuração inválida: '{UserKey}' está ausente ou vazia em ambiente '{environmentName}'. {Hint}");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"Configuração inválida: '{PasswordKey}' está ausente ou vazia em ambiente '{environmentName}'. {Hint}");
        }
    }

    public static void Validate(IConfiguration configuration, IHostEnvironment environment) =>
        Validate(
            environment.EnvironmentName,
            configuration[HostKey],
            configuration[UserKey],
            configuration[PasswordKey]);

    private const string Hint =
        "Sem esta guarda o RabbitMqConnectionProvider cai em 'localhost:5672/guest/guest' e só falha na " +
        "primeira conexão: o serviço sobe verde, o /health/ready passa, e o consumidor nunca conecta ao " +
        "broker real, enquanto o backlog da custodia.prices cresce sem consumidor. Configure via variável " +
        "de ambiente RabbitMq__{Host,User,Password} (Docker/produção) ou via dotnet user-secrets " +
        "(dev local).";
}
