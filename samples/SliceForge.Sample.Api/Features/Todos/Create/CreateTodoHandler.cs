using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Sample.Api.Infrastructure;

namespace SliceForge.Sample.Api.Features.Todos.Create;

internal sealed class CreateTodoHandler(TodoStore todoStore)
    : ICommandHandler<CreateTodoCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(
        CreateTodoCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Guid id = todoStore.Create(command.Title.Trim());
        return Task.FromResult(Result<Guid>.Success(id));
    }
}
