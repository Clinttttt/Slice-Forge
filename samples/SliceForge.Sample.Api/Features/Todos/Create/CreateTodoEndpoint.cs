using SliceForge.AspNetCore.Results;
using SliceForge.Runtime.Messaging;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace SliceForge.Sample.Api.Features.Todos.Create;

internal static class CreateTodoEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/todos",
            async (
                CreateTodoCommand command,
                IMessageSender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.SendCommandAsync<Guid>(command, cancellationToken);
                return result.ToHttpResult(id => HttpResults.Created($"/todos/{id}", new { id }));
            });
    }
}
