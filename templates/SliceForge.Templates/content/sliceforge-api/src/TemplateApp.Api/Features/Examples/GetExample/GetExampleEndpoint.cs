using SliceForge.AspNetCore.Results;
using SliceForge.Runtime.Messaging;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace TemplateApp.Api.Features.Examples.GetExample;

internal static class GetExampleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/examples",
            async (IMessageSender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.SendQueryAsync<ExampleResponse>(
                    new GetExampleQuery(),
                    cancellationToken);

                return result.ToHttpResult(response => HttpResults.Ok(response));
            });
    }
}
