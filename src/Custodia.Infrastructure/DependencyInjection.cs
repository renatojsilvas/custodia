using Custodia.Application.Reparo;
using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Eventos;
using Custodia.Application.Conciliacao;
using Custodia.Application.Liquidacao;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Application.Precos;
using Custodia.Infrastructure.Reparo;
using Custodia.Infrastructure.Common;
using Custodia.Infrastructure.Conciliacao;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Messaging;
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
        services.AddScoped<IPrecoWriteRepository, PrecoWriteRepository>();
        services.AddScoped<IAplicadorIncrementalDePosicao, AplicadorIncrementalDePosicao>();
        services.AddScoped<ICalendarioDiasUteisReadRepository, CalendarioDiasUteisReadRepository>();
        services.AddScoped<IProximoDiaUtilService, ProximoDiaUtilService>();
        services.AddScoped<IMovimentoTravamentoRepository, MovimentoTravamentoRepository>();
        services.AddScoped<IAReceberVencidoReadRepository, AReceberVencidoReadRepository>();
        services.AddScoped<IRepararResgatesAntigosReadRepository, RepararResgatesAntigosReadRepository>();
        services.AddScoped<IConciliacaoDeResgatesReadRepository, ConciliacaoDeResgatesReadRepository>();
        services.AddSingleton<IFilaDeRecalculo, FilaDeRecalculoInerte>();
        services.AddSingleton<IPausaEntreLerEGravar, PausaEntreLerEGravarInerte>();
        services.AddSingleton<RoteadorDeEventos>();
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IPublicadorComConfirmacao, RabbitMqPublicadorComConfirmacao>();
        services.AddSingleton<ConsumidorMetrics>();
        services.AddSingleton<IIdentificadorDePassagem, IdentificadorDePassagemAleatorio>();
        services.AddSingleton<IPontoDeSuspensaoDrenagem, PontoDeSuspensaoDrenagemInerte>();
        services.AddSingleton<IMensagemParkingReprocessador, RoteadorMensagemParkingReprocessador>();
        services.AddSingleton<ParkingDrenadorMetrics>();
        services.AddSingleton<ParkingDrenador>();

        return services;
    }
}
