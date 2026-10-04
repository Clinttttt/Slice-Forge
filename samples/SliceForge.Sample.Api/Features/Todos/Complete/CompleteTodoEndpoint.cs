using SliceForge.AspNetCore.Results;
using SliceForge.Runtime.Messaging;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace SliceForge.Sample.Api.Features.Todos.Complete;

internal static class CompleteTodoEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
            "/todos/{id:guid}/complete",
            async (Guid id, IMessageSender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendCommandAsync(
                    new CompleteTodoCommand(id),
                    cancellationToken);
                return result.ToHttpResult(() => HttpResults.NoContent());
            });
    }
}
