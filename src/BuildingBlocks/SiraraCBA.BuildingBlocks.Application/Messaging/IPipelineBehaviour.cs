namespace SiraraCBA.BuildingBlocks.Application.Messaging;

/// <summary>
/// Continues the pipeline, ultimately reaching the handler. Named a
/// continuation rather than a delegate: CA1711 reserves the <c>Delegate</c>
/// suffix, and "what happens next" is what a behaviour actually holds.
/// </summary>
public delegate Task<TResponse> PipelineContinuation<TResponse>(CancellationToken cancellationToken);

/// <summary>
/// A cross-cutting step wrapped around every request.
/// </summary>
/// <remarks>
/// Registration order is the execution order and is fixed in architecture.md §6:
/// logging, validation, authorization, idempotency, approval, unit of work.
/// Approval sits after authorization and validation deliberately — an invalid or
/// unauthorized command must never be parked for a checker to look at.
/// </remarks>
public interface IPipelineBehaviour<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(
        TRequest request,
        PipelineContinuation<TResponse> next,
        CancellationToken cancellationToken);
}
