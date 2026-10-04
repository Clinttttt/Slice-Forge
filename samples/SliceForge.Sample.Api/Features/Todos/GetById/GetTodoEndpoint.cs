using SliceForge.AspNetCore.Results;
using SliceForge.Runtime.Messaging;
using SliceForge.Sample.Api.Features.Todos;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace SliceForge.Sample.Api.Features.Todos.GetById;

internal static class GetTodoEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/todos/{id:guid}",
            async (Guid id, IMessageSender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendQueryAsync<TodoResponse>(
                    new GetTodoQuery(id),
                    cancellationToken);
                return result.ToHttpResult(todo => HttpResults.Ok(todo));
            });
    }
}
