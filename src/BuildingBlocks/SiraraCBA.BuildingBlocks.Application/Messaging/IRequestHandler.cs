namespace SiraraCBA.BuildingBlocks.Application.Messaging;

/// <summary>
/// Handles exactly one request type. One handler, one use case, one transaction
/// (coding-standards.md, Application).
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
