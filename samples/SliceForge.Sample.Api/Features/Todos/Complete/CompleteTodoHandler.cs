using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Sample.Api.Infrastructure;

namespace SliceForge.Sample.Api.Features.Todos.Complete;

internal sealed class CompleteTodoHandler(TodoStore todoStore)
    : ICommandHandler<CompleteTodoCommand>
{
    public Task<Result> HandleAsync(
        CompleteTodoCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TodoCompletionOutcome outcome = todoStore.Complete(command.Id);
        Result result = outcome switch
        {
            TodoCompletionOutcome.Completed => Result.Success(),
            TodoCompletionOutcome.NotFound => Result.Failure(
                new Error("Todos.NotFound", "Todo was not found.", ErrorType.NotFound)),
            TodoCompletionOutcome.AlreadyCompleted => Result.Failure(
                new Error(
                    "Todos.AlreadyCompleted",
                    "Todo is already completed.",
                    ErrorType.Conflict)),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown todo outcome.")
        };

        return Task.FromResult(result);
    }
}
