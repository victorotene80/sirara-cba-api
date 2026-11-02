namespace SiraraCBA.BuildingBlocks.Application.Messaging;

/// <summary>
/// Dispatches a request through the pipeline to its handler.
/// </summary>
/// <remarks>
/// This abstraction exists in-house rather than being taken from a mediator
/// library, for two reasons. Application code should not carry a third-party type
/// in the signature of every use case — that is a dependency the whole system
/// inherits and cannot cheaply remove. And a bank should not have a licensing
/// question sitting under its transaction pipeline. The implementation behind this
/// interface is roughly a hundred lines; swapping it for a library later changes
/// one registration and no call sites. See decisions.md D-14.
/// </remarks>
public interface ISender
{
    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);
}
