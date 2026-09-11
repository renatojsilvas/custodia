using Custodia.Application.Common.Interfaces;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Infrastructure.Observability;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
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
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IApiKeyMetrics, ApiKeyMetrics>();
        services.AddSingleton<IBusinessMetrics, BusinessMetrics>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMovimentoReadRepository, MovimentoReadRepository>();
        services.AddScoped<IMovimentoWriteRepository, MovimentoWriteRepository>();
        services.AddScoped<IPosicaoCorrenteReadRepository, PosicaoCorrenteReadRepository>();
        services.AddScoped<IPosicaoCorrenteWriteRepository, PosicaoCorrenteWriteRepository>();

        return services;
    }
}
