using SliceForge.Messaging;

namespace SliceForge.Sample.Api.Features.Todos.Create;

internal sealed record CreateTodoCommand(string Title) : ICommand<Guid>;
