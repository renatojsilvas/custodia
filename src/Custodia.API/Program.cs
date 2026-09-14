using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Prometheus;
using Custodia.API;
using Custodia.API.Cli;
using Custodia.API.Extensions;
using Custodia.API.Middleware;
using Custodia.Application;
using Custodia.Application.Reparo;
using Custodia.Application.Posicoes;
using Custodia.Application.Precos.Bootstrap;
using Custodia.Domain.Common;
using Custodia.Domain.Eventos;
using Custodia.Infrastructure;
using Custodia.Infrastructure.Calendario;
using Custodia.Infrastructure.Conciliacao;
using Custodia.Infrastructure.Liquidacao;
using Custodia.Infrastructure.Messaging;
using MediatR;
using IResult = Microsoft.AspNetCore.Http.IResult;

const string VerboReconstruirPosicoes = "--reconstruir-posicoes";
const string VerboDrenarParking = "--drenar-parking";
const string VerboRepararResgatesAntigos = "--reparar-resgates-antigos";
const string VerboBootstrapPrecos = "--bootstrap-precos";
const string ArgumentoPassagemId = "--passagem-id";
const int CodigoDeSaidaUso = 64;

if (args.Length > 0
    && (args[0] == VerboReconstruirPosicoes || args[0] == VerboDrenarParking
        || args[0] == VerboRepararResgatesAntigos || args[0] == VerboBootstrapPrecos))
{
    var codigoDeSaidaAdmin = await ExecutarComandoAdministrativoAsync(args);
    Environment.Exit(codigoDeSaidaAdmin);
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilog();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices();
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
const string ChaveConfiguracaoLiquidacaoJobHabilitado = "Decisao:LiquidacaoJobHabilitado";

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<RabbitMqTradeConsumidor>();
    builder.Services.AddHostedService<RabbitMqFilaProfundidadePoller>();
    builder.Services.AddHostedService<CalendarioDiasUteisHorizonteGuard>();
    builder.Services.AddHostedService<ConciliacaoDeResgatesJob>();
    builder.Services.AddHostedService<ConciliacaoDePrecosJob>();

    if (builder.Configuration.GetValue(ChaveConfiguracaoLiquidacaoJobHabilitado, defaultValue: true))
    {
        builder.Services.AddHostedService<LiquidacaoDeResgatesJob>();
    }
}
var app = builder.Build();
NormalizeApiKeyConfiguration(app.Configuration);
ConnectionStringGuard.Validate(app.Configuration, app.Environment);
ApiKeyGuard.Validate(app.Configuration, app.Environment);
RabbitMqConfigGuard.Validate(app.Configuration, app.Environment);
HubConfigGuard.Validate(app.Configuration, app.Environment);
BootstrapPrecosConfigGuard.Validate(app.Configuration);
await app.InitializeDatabaseAsync();
app.UseForwardedHeaders();
var httpMetricsExcludedPaths = app.Configuration.GetSection("Metrics:ExcludedPaths").Get<string[]>() ?? [];
app.UseWhen(
    ctx => !httpMetricsExcludedPaths.Any(p =>
        ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)),
    branch => branch.UseHttpMetrics());
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = exception is BadHttpRequestException badHttpRequestException
            ? badHttpRequestException.StatusCode
            : StatusCodes.Status500InternalServerError;
        var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new Microsoft.AspNetCore.Http.ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = context.Response.StatusCode },
        });
    });
});
app.UseSerilogDefaults();
app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapMetrics();
if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/_test/throw", IResult () => throw new InvalidOperationException("Forced exception for exception handler testing."))
        .ExcludeFromDescription();
    app.MapGet("/_test/result/validation", IResult () =>
            Result.Failure(new Error("Test.Validation", "Validation failure for testing.", ErrorType.Validation))
                .ToHttpResult(() => Results.Ok()))
        .ExcludeFromDescription();
    app.MapGet("/_test/result/not-found", IResult () =>
            Result.Failure(new Error("Test.NotFound", "Not found for testing.", ErrorType.NotFound))
                .ToHttpResult(() => Results.Ok()))
        .ExcludeFromDescription();
    app.MapGet("/_test/result/conflict", IResult () =>
            Result.Failure(new Error("Test.Conflict", "Conflict for testing.", ErrorType.Conflict))
                .ToHttpResult(() => Results.Ok()))
        .ExcludeFromDescription();
    app.MapGet("/_test/result/success", IResult () =>
            Result.Success().ToHttpResult(() => Results.Ok(new { ok = true })))
        .ExcludeFromDescription();
}
app.Run();
static void NormalizeApiKeyConfiguration(IConfiguration configuration)
{
    var rawKey = configuration["ApiKey:Key"];
    if (rawKey is not null)
    {
        configuration["ApiKey:Key"] = rawKey.Trim();
    }
}

static async Task<int> ExecutarComandoAdministrativoAsync(string[] args)
{
    var adminBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
    });

    adminBuilder.Services.AddApplication();
    adminBuilder.Services.AddInfrastructure(adminBuilder.Configuration);
    adminBuilder.Services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
    adminBuilder.Services.AddScoped<IDatabaseMigrator, EfCoreDatabaseMigrator>();

    ConnectionStringGuard.Validate(adminBuilder.Configuration, adminBuilder.Environment);
    RabbitMqConfigGuard.Validate(adminBuilder.Configuration, adminBuilder.Environment);

    if (args[0] == VerboBootstrapPrecos)
    {
        HubConfigGuard.Validate(adminBuilder.Configuration, adminBuilder.Environment);
        BootstrapPrecosConfigGuard.Validate(adminBuilder.Configuration);
    }

    using var adminHost = adminBuilder.Build();

    await adminHost.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

    return args[0] switch
    {
        VerboReconstruirPosicoes => await ExecutarReconstruirPosicoesAsync(adminHost.Services, args),
        VerboDrenarParking => await ExecutarDrenarParkingAsync(adminHost.Services, args),
        VerboRepararResgatesAntigos => await ExecutarRepararResgatesAntigosAsync(adminHost.Services),
        VerboBootstrapPrecos => await ExecutarBootstrapPrecosAsync(adminHost.Services, args),
        _ => CodigoDeSaidaUso,
    };
}

static async Task<int> ExecutarRepararResgatesAntigosAsync(IServiceProvider servicos)
{
    using var escopo = servicos.CreateScope();
    var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
    var resultado = await mediator.Send(new RepararResgatesAntigosCommand());

    if (resultado.IsFailure)
    {
        Console.WriteLine(
            $"DESFECHO=FALHA CODIGO={resultado.Error.Code} MENSAGEM=\"{resultado.Error.Description}\"");
        return 1;
    }

    Console.WriteLine(
        $"DESFECHO=SUCESSO RESGATES_CANDIDATOS={resultado.Value.ResgatesCandidatos} " +
        $"RESGATES_BACKFILLED={resultado.Value.ResgatesBackfilled} " +
        $"AJUSTES_CANDIDATOS={resultado.Value.AjustesCandidatos} " +
        $"AJUSTES_BACKFILLED={resultado.Value.AjustesBackfilled}");
    return 0;
}

static async Task<int> ExecutarReconstruirPosicoesAsync(IServiceProvider servicos, string[] args)
{
    var clienteId = args.Length > 1 ? args[1] : null;
    var instrumentoId = args.Length > 2 ? args[2] : null;

    using var escopo = servicos.CreateScope();
    var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
    var resultado = await mediator.Send(new ReconstruirPosicoesCommand(clienteId, instrumentoId));

    if (resultado.IsFailure)
    {
        Console.WriteLine(
            $"DESFECHO=FALHA CODIGO={resultado.Error.Code} MENSAGEM=\"{resultado.Error.Description}\"");
        return 1;
    }

    Console.WriteLine(
        $"DESFECHO=SUCESSO CHAVES_RECONSTRUIDAS={resultado.Value.ChavesReconstruidas} " +
        $"CHAVES_REMOVIDAS_POR_ORFANDADE={resultado.Value.ChavesRemovidasPorOrfandade}");
    return 0;
}

static async Task<int> ExecutarDrenarParkingAsync(IServiceProvider servicos, string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine($"Uso: {VerboDrenarParking} <motivo> [{ArgumentoPassagemId} <id>]");
        return CodigoDeSaidaUso;
    }

    var motivoResult = MotivoParking.FromName(args[1]);
    if (motivoResult.IsFailure)
    {
        Console.Error.WriteLine($"Motivo de estacionamento desconhecido: '{args[1]}'.");
        return CodigoDeSaidaUso;
    }

    string? passagemIdForcado = null;
    for (var i = 2; i < args.Length - 1; i++)
    {
        if (args[i] == ArgumentoPassagemId)
        {
            passagemIdForcado = args[i + 1];
        }
    }

    var drenador = servicos.GetRequiredService<ParkingDrenador>();
    var resultado = await drenador.DrenarAsync(motivoResult.Value, passagemIdForcado, CancellationToken.None);

    Console.WriteLine(
        $"DESFECHO={resultado.Desfecho} MOTIVO={resultado.Motivo} PASSAGEM_ID={resultado.PassagemId} " +
        $"N={resultado.Estoque} TETO={resultado.Teto} N_MOTIVO={resultado.NMotivo} " +
        $"RESIDUAL_MOTIVO={resultado.ResidualMotivo}");

    return resultado.Desfecho switch
    {
        DesfechoDrenagem.Completude => 0,
        DesfechoDrenagem.Parcial => 2,
        DesfechoDrenagem.VazioDoMotivo => 3,
        DesfechoDrenagem.LimitePorTeto => 4,
        DesfechoDrenagem.LimitePorVolta => 5,
        DesfechoDrenagem.Interrompida => 6,
        _ => 1,
    };
}

static async Task<int> ExecutarBootstrapPrecosAsync(IServiceProvider servicos, string[] args)
{
    var argumentos = BootstrapPrecosArgumentos.Interpretar(args);
    if (!argumentos.EhValido)
    {
        Console.Error.WriteLine(
            $"Uso: {VerboBootstrapPrecos} [{BootstrapPrecosArgumentos.ArgumentoDesde} <data>] " +
            $"[{BootstrapPrecosArgumentos.ArgumentoAte} <data>] — {argumentos.ErroDeUso}");
        return CodigoDeSaidaUso;
    }

    using var escopo = servicos.CreateScope();
    var mediator = escopo.ServiceProvider.GetRequiredService<IMediator>();
    var resultado = await mediator.Send(
        new ColetarPrecosDoHubCommand(EscopoDeColetaDePrecos.LivroInteiroNaJanela, argumentos.Desde, argumentos.Ate));

    if (resultado.IsFailure)
    {
        Console.WriteLine(
            $"DESFECHO=FALHA CODIGO={resultado.Error.Code} MENSAGEM=\"{resultado.Error.Description}\"");
        return 1;
    }

    var valor = resultado.Value;
    var houveAnomalia = valor.InstrumentosDesconhecidos > 0 || valor.CampoPosicaoNaoInformado > 0 || valor.ValoresDivergentes > 0;

    Console.WriteLine(
        $"DESFECHO={(houveAnomalia ? "COMPLETUDE_COM_ANOMALIA" : "COMPLETUDE")} DIAS={valor.Dias} " +
        $"CHAMADAS={valor.ChamadasHttp} INSTRUMENTOS={valor.Instrumentos} HISTORICO_INSERIDO={valor.HistoricoInserido} " +
        $"PRECO_ATUAL_CRIADO={valor.PrecoAtualCriado} PRECO_ATUAL_ATUALIZADO={valor.PrecoAtualAtualizado} " +
        $"PRECO_ATUAL_CAMPO_TROCADO={valor.PrecoAtualCampoTrocado} SEM_PRECO={valor.SemPreco} " +
        $"CAMPO_POSICAO_SEM_PRECO={valor.CampoPosicaoSemPreco} CAMPO_POSICAO_NAO_INFORMADO={valor.CampoPosicaoNaoInformado} " +
        $"DESCONHECIDOS={valor.InstrumentosDesconhecidos} VALORES_DIVERGENTES={valor.ValoresDivergentes} " +
        $"REVISOES_MAIORES_QUE_ZERO={valor.RevisoesMaioresQueZero}");

    return houveAnomalia ? 2 : 0;
}

public partial class Program;
