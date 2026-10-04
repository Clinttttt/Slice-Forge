using FluentValidation;

namespace SliceForge.Sample.Api.Features.Todos.Create;

internal sealed class CreateTodoValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoValidator()
    {
        RuleFor(command => command.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title must not be blank.")
            .MaximumLength(120);
    }
}
