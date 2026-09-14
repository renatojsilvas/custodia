using Custodia.Application.Precos.Bootstrap;
using Custodia.Domain.Common;
using MediatR;

namespace Custodia.Infrastructure.Tests.Conciliacao;

internal sealed class FakeMediatorParaConciliacaoDePrecos(Result<ResultadoColetaDePrecos> resultado) : IMediator
{
    public List<ColetarPrecosDoHubCommand> ComandosRecebidos { get; } = [];

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is ColetarPrecosDoHubCommand comando)
        {
            ComandosRecebidos.Add(comando);
            return Task.FromResult((TResponse)(object)resultado);
        }

        throw new NotSupportedException($"FakeMediatorParaConciliacaoDePrecos não sabe responder a {request.GetType()}.");
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        throw new NotSupportedException();

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification =>
        throw new NotSupportedException();
}
