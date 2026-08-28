using MediatR;

namespace MyFrete.BuildingBlocks.Application;

/// <summary>A state-changing use case. Wrapped in a unit of work by the pipeline.</summary>
public interface ICommand : IRequest;

public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A read-only use case. Not wrapped in a unit of work.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
