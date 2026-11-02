using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SiraraCBA.BuildingBlocks.Application.Messaging;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Resolves a request's handler, wraps it in the registered pipeline behaviours,
/// and executes it.
/// </summary>
/// <remarks>
/// Behaviours are applied in registration order, so the first registered behaviour
/// is the outermost. The per-request-type executor is built once and cached, so the
/// reflection cost is paid on first use and never again.
/// </remarks>
public sealed class RequestDispatcher : ISender
{
    private static readonly ConcurrentDictionary<Type, Executor> Cache = new();

    private readonly IServiceProvider _services;

    public RequestDispatcher(IServiceProvider services) => _services = services;

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Executor executor = Cache.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) => Create(requestType, responseType),
            typeof(TResponse));

        return ((Executor<TResponse>)executor).ExecuteAsync(request, _services, cancellationToken);
    }

    private static Executor Create(Type requestType, Type responseType)
    {
        Type executorType = typeof(Executor<,>).MakeGenericType(requestType, responseType);

        return (Executor)Activator.CreateInstance(executorType)!;
    }

    private abstract class Executor;

    private abstract class Executor<TResponse> : Executor
    {
        public abstract Task<TResponse> ExecuteAsync(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    private sealed class Executor<TRequest, TResponse> : Executor<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> ExecuteAsync(
            object request,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            PipelineContinuation<TResponse> pipeline = ct => handler.HandleAsync(typedRequest, ct);

            // Reversed so that the first-registered behaviour ends up outermost.
            foreach (var behaviour in services
                         .GetServices<IPipelineBehaviour<TRequest, TResponse>>()
                         .Reverse())
            {
                PipelineContinuation<TResponse> next = pipeline;
                IPipelineBehaviour<TRequest, TResponse> current = behaviour;
                pipeline = ct => current.HandleAsync(typedRequest, next, ct);
            }

            return pipeline(cancellationToken);
        }
    }
}
