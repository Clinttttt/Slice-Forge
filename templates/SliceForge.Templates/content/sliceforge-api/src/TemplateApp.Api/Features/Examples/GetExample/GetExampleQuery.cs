using SliceForge.Messaging;

namespace TemplateApp.Api.Features.Examples.GetExample;

internal sealed record GetExampleQuery : IQuery<ExampleResponse>;

internal sealed record ExampleResponse(string Message);
