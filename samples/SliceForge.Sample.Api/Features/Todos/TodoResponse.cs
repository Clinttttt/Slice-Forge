namespace SliceForge.Sample.Api.Features.Todos;

internal sealed record TodoResponse(Guid Id, string Title, bool IsCompleted);
