using SliceForge.Messaging;
using SliceForge.Sample.Api.Features.Todos;

namespace SliceForge.Sample.Api.Features.Todos.GetById;

internal sealed record GetTodoQuery(Guid Id) : IQuery<TodoResponse>;
