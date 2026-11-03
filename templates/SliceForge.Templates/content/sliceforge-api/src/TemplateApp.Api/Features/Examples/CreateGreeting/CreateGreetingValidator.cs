using FluentValidation;

namespace TemplateApp.Api.Features.Examples.CreateGreeting;

internal sealed class CreateGreetingValidator : AbstractValidator<CreateGreetingCommand>
{
    public CreateGreetingValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.");

        RuleFor(command => command.Name)
            .MaximumLength(100)
            .WithMessage("Name must be 100 characters or fewer.");
    }
}
