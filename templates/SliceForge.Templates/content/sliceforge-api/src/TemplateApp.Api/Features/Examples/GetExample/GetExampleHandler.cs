using SliceForge.Messaging;
using SliceForge.Results;

namespace TemplateApp.Api.Features.Examples.GetExample;

internal sealed class GetExampleHandler : IQueryHandler<GetExampleQuery, ExampleResponse>
{
    public Task<Result<ExampleResponse>> HandleAsync(
        GetExampleQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Result<ExampleResponse>.Success(new ExampleResponse("SliceForge is ready.")));
    }
}
