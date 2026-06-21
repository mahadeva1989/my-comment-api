using MediatR;

namespace my_comment_api.CQRS;

public interface ICommand<TResult> : IRequest<TResult>{}
public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, TResult> where TCommand: ICommand<TResult>{}