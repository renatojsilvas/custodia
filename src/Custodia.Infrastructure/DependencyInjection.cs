using Custodia.Application.Reparo;
using Custodia.Application.Calendario;
using Custodia.Application.Common.Interfaces;
using Custodia.Application.Eventos;
using Custodia.Application.Conciliacao;
using Custodia.Application.Liquidacao;
using Custodia.Application.Movimentos;
using Custodia.Application.Posicoes;
using Custodia.Application.Precos;
using Custodia.Application.Precos.Bootstrap;
using Custodia.Application.Precos.Hub;
using Custodia.Infrastructure.Reparo;
using Custodia.Infrastructure.Common;
using Custodia.Infrastructure.Conciliacao;
using Custodia.Infrastructure.Hub;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Messaging;
using Custodia.Infrastructure.Observability;
using Custodia.Infrastructure.Persistence;
using Custodia.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Npgsql;
using Polly;

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
        services.AddScoped<IEscopoDePrecosReadRepository, EscopoDePrecosReadRepository>();
        services.AddHttpClient<IHubPrecosClient, HubPrecosClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["Hub:BaseUrl"];
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps))
            {
                client.BaseAddress = baseUri;
            }

            var apiKey = config["Hub:ApiKey"]?.Trim();
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
        })
        .AddHubPrecosResilienceHandler(configuration);
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

    public static IHttpResiliencePipelineBuilder AddHubPrecosResilienceHandler(
        this IHttpClientBuilder builder, IConfiguration configuration)
    {
        return builder.AddResilienceHandler("hub-precos-resilience", pipeline =>
        {
            var section = configuration.GetSection("Resilience:Hub");
            var totalTimeout = section.GetValue<TimeSpan?>("TotalTimeout") ?? TimeSpan.FromSeconds(5);
            var retryMaxAttempts = section.GetValue<int?>("Retry:MaxAttempts") ?? 2;
            var retryBaseDelay = section.GetValue<TimeSpan?>("Retry:BaseDelay") ?? TimeSpan.FromMilliseconds(200);
            var failureRatio = section.GetValue<double?>("CircuitBreaker:FailureRatio") ?? 0.5;
            var minimumThroughput = section.GetValue<int?>("CircuitBreaker:MinimumThroughput") ?? 10;
            var samplingDuration = section.GetValue<TimeSpan?>("CircuitBreaker:SamplingDuration") ?? TimeSpan.FromSeconds(30);
            var breakDuration = section.GetValue<TimeSpan?>("CircuitBreaker:BreakDuration") ?? TimeSpan.FromSeconds(15);
            var attemptTimeout = section.GetValue<TimeSpan?>("AttemptTimeout") ?? TimeSpan.FromSeconds(2);
            pipeline
                .AddTimeout(new HttpTimeoutStrategyOptions { Timeout = totalTimeout })
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = retryMaxAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = retryBaseDelay,
                    ShouldHandle = static args =>
                        ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome))
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = failureRatio,
                    MinimumThroughput = minimumThroughput,
                    SamplingDuration = samplingDuration,
                    BreakDuration = breakDuration,
                    ShouldHandle = static args =>
                        ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome))
                })
                .AddTimeout(new HttpTimeoutStrategyOptions { Timeout = attemptTimeout });
        });
    }
}
