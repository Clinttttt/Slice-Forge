using SliceForge.Messaging;

namespace SliceForge.Sample.Api.Features.Todos.Complete;

internal sealed record CompleteTodoCommand(Guid Id) : ICommand;
