using SliceForge.Messaging;
using SliceForge.Results;

namespace TemplateApp.Api.Features.Examples.CreateGreeting;

internal sealed class CreateGreetingHandler : ICommandHandler<CreateGreetingCommand, GreetingResponse>
{
    public Task<Result<GreetingResponse>> HandleAsync(
        CreateGreetingCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        GreetingResponse response = new($"Hello, {command.Name}!");
        return Task.FromResult(Result<GreetingResponse>.Success(response));
    }
}
