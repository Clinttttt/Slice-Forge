using SliceForge.AspNetCore.Results;
using SliceForge.Runtime.Messaging;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace TemplateApp.Api.Features.Examples.CreateGreeting;

internal static class CreateGreetingEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/examples/greetings",
            async (CreateGreetingCommand command, IMessageSender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendCommandAsync<GreetingResponse>(command, cancellationToken);

                return result.ToHttpResult(response => HttpResults.Ok(response));
            });
    }
}
