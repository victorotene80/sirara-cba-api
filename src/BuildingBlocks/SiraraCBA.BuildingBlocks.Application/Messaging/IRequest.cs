namespace SiraraCBA.BuildingBlocks.Application.Messaging;

/// <summary>A message with exactly one handler, returning <typeparamref name="TResponse"/>.</summary>
public interface IRequest<out TResponse>;

/// <summary>A request that changes state.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A request that changes state and returns only success or failure.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>A request that reads state and changes nothing.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
