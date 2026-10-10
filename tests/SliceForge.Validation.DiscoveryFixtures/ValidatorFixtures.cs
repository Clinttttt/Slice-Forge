using FluentValidation;
using FluentValidation.Results;
using SliceForge.Messaging;

namespace SliceForge.Validation.DiscoveryFixtures;

public sealed record InternalValidatedCommand(string Value) : ICommand;

public sealed record PublicValidatedCommand(string Value) : ICommand<Guid>;

public sealed record QueryValidatedMessage(string Value) : IQuery<string>;

public sealed record OrderedValidatedCommand(string Value) : ICommand;

public sealed record DependencyValidatedCommand(string Value) : ICommand;

public sealed record DirectInterfaceValidatedCommand(string Value) : ICommand;

public sealed record FirstMultiContractCommand(string Value) : ICommand;

public sealed record SecondMultiContractCommand(string Value) : ICommand;

public sealed record AbstractValidatorTargetCommand(string Value) : ICommand;

public sealed record GenericValidatedCommand<T>(T Value) : ICommand;

public sealed record UnvalidatedDiscoveryCommand : ICommand;

public record ValidationBaseCommand(string Value) : ICommand;

public sealed record ValidationDerivedCommand(string Value) : ValidationBaseCommand(Value);

public sealed class ValidatorDiscoveryProbe
{
    public int ValidationCount { get; set; }

    public List<string> OrderedCalls { get; } = [];

    public List<string> MultiContractCalls { get; } = [];

    public object? LastScopedDependency { get; set; }
}

public sealed class ScopedValidatorDependency;

internal sealed class InternalValidatedCommandValidator : AbstractValidator<InternalValidatedCommand>
{
    public InternalValidatedCommandValidator(ValidatorDiscoveryProbe probe) : base()
    {
        RuleFor(command => command.Value)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Value is required.");
        RuleFor(command => command.Value).Must((_, _) =>
        {
            probe.ValidationCount++;
            return true;
        });
    }
}

public sealed class PublicValidatedCommandValidator : AbstractValidator<PublicValidatedCommand>
{
    public PublicValidatedCommandValidator() =>
        RuleFor(command => command.Value).NotEmpty().WithMessage("Value is required.");
}

internal sealed class QueryValidatedMessageValidator : AbstractValidator<QueryValidatedMessage>
{
    public QueryValidatedMessageValidator() =>
        RuleFor(query => query.Value).Equal("valid").WithMessage("Query value is invalid.");
}

internal sealed class AlphaOrderedValidatedCommandValidator : AbstractValidator<OrderedValidatedCommand>
{
    public AlphaOrderedValidatedCommandValidator(ValidatorDiscoveryProbe probe) : base()
    {
        RuleFor(command => command.Value)
            .Must((command, _) =>
            {
                probe.OrderedCalls.Add("Alpha");
                return command.Value != "invalid";
            })
            .WithMessage("Shared failure.");
    }
}

internal sealed class ZetaOrderedValidatedCommandValidator : AbstractValidator<OrderedValidatedCommand>
{
    public ZetaOrderedValidatedCommandValidator(ValidatorDiscoveryProbe probe) : base()
    {
        RuleFor(command => command.Value)
            .Must((command, _) =>
            {
                probe.OrderedCalls.Add("Zeta");
                return command.Value != "invalid";
            })
            .WithMessage("Shared failure.");
        RuleFor(command => command.Value)
            .Must((command, _) => command.Value != "invalid")
            .WithMessage("Zeta failure.");
    }
}

internal sealed class DependencyValidatedCommandValidator : AbstractValidator<DependencyValidatedCommand>
{
    public DependencyValidatedCommandValidator(
        ScopedValidatorDependency dependency,
        ValidatorDiscoveryProbe probe) : base()
    {
        probe.LastScopedDependency = dependency;
        RuleFor(command => command.Value).NotEmpty();
    }
}

internal sealed class DirectInterfaceValidatedCommandValidator : IValidator<DirectInterfaceValidatedCommand>
{
    private readonly IValidator<DirectInterfaceValidatedCommand> _inner;

    public DirectInterfaceValidatedCommandValidator()
    {
        InlineValidator<DirectInterfaceValidatedCommand> validator = new();
        validator.RuleFor(command => command.Value).NotEmpty();
        _inner = validator;
    }

    public ValidationResult Validate(DirectInterfaceValidatedCommand instance) => _inner.Validate(instance);

    public Task<ValidationResult> ValidateAsync(
        DirectInterfaceValidatedCommand instance,
        CancellationToken cancellation) =>
        _inner.ValidateAsync(instance, cancellation);

    ValidationResult IValidator.Validate(IValidationContext context) =>
        ((IValidator)_inner).Validate(context);

    Task<ValidationResult> IValidator.ValidateAsync(
        IValidationContext context,
        CancellationToken cancellation) =>
        ((IValidator)_inner).ValidateAsync(context, cancellation);

    IValidatorDescriptor IValidator.CreateDescriptor() => ((IValidator)_inner).CreateDescriptor();

    bool IValidator.CanValidateInstancesOfType(Type type) =>
        ((IValidator)_inner).CanValidateInstancesOfType(type);
}

internal abstract class MultiContractValidatorBase :
    IValidator<FirstMultiContractCommand>,
    IValidator<SecondMultiContractCommand>
{
    private readonly IValidator<FirstMultiContractCommand> _first;
    private readonly IValidator<SecondMultiContractCommand> _second;

    protected MultiContractValidatorBase(
        IValidator<FirstMultiContractCommand> first,
        IValidator<SecondMultiContractCommand> second)
    {
        _first = first;
        _second = second;
    }

    ValidationResult IValidator<FirstMultiContractCommand>.Validate(FirstMultiContractCommand instance) =>
        _first.Validate(instance);

    Task<ValidationResult> IValidator<FirstMultiContractCommand>.ValidateAsync(
        FirstMultiContractCommand instance,
        CancellationToken cancellation) =>
        _first.ValidateAsync(instance, cancellation);

    ValidationResult IValidator<SecondMultiContractCommand>.Validate(SecondMultiContractCommand instance) =>
        _second.Validate(instance);

    Task<ValidationResult> IValidator<SecondMultiContractCommand>.ValidateAsync(
        SecondMultiContractCommand instance,
        CancellationToken cancellation) =>
        _second.ValidateAsync(instance, cancellation);

    ValidationResult IValidator.Validate(IValidationContext context) =>
        context.InstanceToValidate is FirstMultiContractCommand
            ? ((IValidator)_first).Validate(context)
            : ((IValidator)_second).Validate(context);

    Task<ValidationResult> IValidator.ValidateAsync(
        IValidationContext context,
        CancellationToken cancellation) =>
        context.InstanceToValidate is FirstMultiContractCommand
            ? ((IValidator)_first).ValidateAsync(context, cancellation)
            : ((IValidator)_second).ValidateAsync(context, cancellation);

    IValidatorDescriptor IValidator.CreateDescriptor() => _first.CreateDescriptor();

    bool IValidator.CanValidateInstancesOfType(Type type) =>
        ((IValidator)_first).CanValidateInstancesOfType(type) ||
        ((IValidator)_second).CanValidateInstancesOfType(type);
}

internal sealed class MultiContractValidator : MultiContractValidatorBase
{
    public MultiContractValidator(ValidatorDiscoveryProbe probe)
        : base(CreateFirst(probe), CreateSecond(probe))
    {
    }

    private static IValidator<FirstMultiContractCommand> CreateFirst(ValidatorDiscoveryProbe probe)
    {
        InlineValidator<FirstMultiContractCommand> validator = new();
        validator.RuleFor(command => command.Value).Must((_, _) =>
        {
            probe.MultiContractCalls.Add("first");
            return true;
        });
        return validator;
    }

    private static IValidator<SecondMultiContractCommand> CreateSecond(ValidatorDiscoveryProbe probe)
    {
        InlineValidator<SecondMultiContractCommand> validator = new();
        validator.RuleFor(command => command.Value).Must((_, _) =>
        {
            probe.MultiContractCalls.Add("second");
            return true;
        });
        return validator;
    }
}

internal abstract class AbstractValidatorFixture : AbstractValidator<AbstractValidatorTargetCommand>
{
}

internal sealed class OpenGenericCommandValidator<T> : AbstractValidator<GenericValidatedCommand<T>>
{
    public OpenGenericCommandValidator() =>
        RuleFor(command => command.Value).Must(_ => false).WithMessage("Open generic validator ran.");
}

public sealed class UnrelatedFixtureType;

internal sealed class UnvalidatedDiscoveryCommandHandler : ICommandHandler<UnvalidatedDiscoveryCommand>
{
    public Task<SliceForge.Results.Result> HandleAsync(
        UnvalidatedDiscoveryCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(SliceForge.Results.Result.Success());
}

internal sealed class ValidationBaseCommandValidator : AbstractValidator<ValidationBaseCommand>
{
    public ValidationBaseCommandValidator() =>
        RuleFor(command => command.Value).Must(_ => false).WithMessage("Base message was used.");
}
