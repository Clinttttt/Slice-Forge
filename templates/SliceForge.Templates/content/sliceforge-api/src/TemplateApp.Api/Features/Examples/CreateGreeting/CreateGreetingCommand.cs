using SliceForge.Messaging;

namespace TemplateApp.Api.Features.Examples.CreateGreeting;

internal sealed record CreateGreetingCommand(string Name) : ICommand<GreetingResponse>;

internal sealed record GreetingResponse(string Message);
