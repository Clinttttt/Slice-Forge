using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Sample.Api.Features.Todos;
using SliceForge.Sample.Api.Infrastructure;

namespace SliceForge.Sample.Api.Features.Todos.GetById;

internal sealed class GetTodoHandler(TodoStore todoStore)
    : IQueryHandler<GetTodoQuery, TodoResponse>
{
    public Task<Result<TodoResponse>> HandleAsync(
        GetTodoQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TodoResponse? todo = todoStore.Find(query.Id);
        Result<TodoResponse> result = todo is null
            ? Result<TodoResponse>.Failure(
                new Error("Todos.NotFound", "Todo was not found.", ErrorType.NotFound))
            : Result<TodoResponse>.Success(todo);

        return Task.FromResult(result);
    }
}
