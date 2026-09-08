using Custodia.Infrastructure.Observability;
using Custodia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Custodia.Infrastructure;

public static class DependencyInjection
{
    private const int NpgsqlMaxPoolSize = 5;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(
            configuration.GetConnectionString("DefaultConnection")!)
        {
            NoResetOnClose = true,
            MaxPoolSize = NpgsqlMaxPoolSize
        }.ConnectionString;
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<IApiKeyMetrics, ApiKeyMetrics>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
