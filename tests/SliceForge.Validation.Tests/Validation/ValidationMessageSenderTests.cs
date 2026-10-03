using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Runtime.Messaging;
using SliceForge.Validation.DependencyInjection;

namespace SliceForge.Validation.Tests.Validation;

public sealed class ValidationMessageSenderTests
{
    [Fact]
    public async Task Valid_non_generic_command_executes_handler_once()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, PassingDeleteValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("valid"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Valid_typed_command_preserves_inner_result()
    {
        Guid expected = Guid.NewGuid();
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<CreateCommand, Guid, CreateHandler>()
                .AddSliceForgeValidator<CreateCommand, PassingCreateValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            CreateHandler.Expected = expected;
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result<Guid> result = await sender.SendCommandAsync(
                new CreateCommand("valid"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
            Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Valid_query_preserves_inner_result()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeQueryHandler<UserQuery, string, UserQueryHandler>()
                .AddSliceForgeValidator<UserQuery, PassingUserQueryValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result<string> result = await sender.SendQueryAsync(
                new UserQuery("valid"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Ada", result.Value);
            Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Invalid_non_generic_command_returns_validation_failure_and_skips_handler()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, FailingDeleteValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("invalid"), CancellationToken.None);

            AssertValidationFailure(result, "Value", "The value is invalid.");
            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Invalid_typed_command_returns_typed_validation_failure_and_skips_handler()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<CreateCommand, Guid, CreateHandler>()
                .AddSliceForgeValidator<CreateCommand, FailingCreateValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result<Guid> result = await sender.SendCommandAsync(
                new CreateCommand("invalid"),
                CancellationToken.None);

            AssertValidationFailure(result, "Value", "The create value is invalid.");
            Assert.Equal(typeof(Result<Guid>), result.GetType());
            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Invalid_query_returns_typed_validation_failure_and_skips_handler()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeQueryHandler<UserQuery, string, UserQueryHandler>()
                .AddSliceForgeValidator<UserQuery, FailingUserQueryValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result<string> result = await sender.SendQueryAsync(
                new UserQuery("invalid"),
                CancellationToken.None);

            AssertValidationFailure(result, "Value", "The query value is invalid.");
            Assert.Equal(typeof(Result<string>), result.GetType());
            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task No_validator_route_executes_inner_sender_once()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("unvalidated"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Validator_registration_after_enablement_is_used()
    {
        ServiceCollection services = CreateBaseServices();
        services.AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>();
        services.AddSliceForgeValidation();
        services.AddSliceForgeValidator<DeleteCommand, FailingDeleteValidator>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result result = await sender.SendCommandAsync(new DeleteCommand("invalid"), CancellationToken.None);

        AssertValidationFailure(result, "Value", "The value is invalid.");
        Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
    }

    [Fact]
    public async Task Multiple_validators_execute_in_registration_order()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, FirstPassingValidator>()
                .AddSliceForgeValidator<DeleteCommand, SecondPassingValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.SendCommandAsync(new DeleteCommand("valid"), CancellationToken.None);

            Assert.Equal(
                new[] { "first", "second" },
                scope.ServiceProvider.GetRequiredService<TestState>().ValidatorOrder);
        }
    }

    [Fact]
    public async Task Multiple_validator_failures_are_aggregated_without_duplicates()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, FirstFailingValidator>()
                .AddSliceForgeValidator<DeleteCommand, SecondFailingValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("invalid"), CancellationToken.None);

            AssertValidationFailure(
                result,
                "Value",
                "First failure.",
                "Second failure.");
            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Object_level_failures_preserve_empty_property_name()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, ObjectLevelValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("invalid"), CancellationToken.None);

            AssertValidationFailure(result, string.Empty, "Object-level failure.");
        }
    }

    [Fact]
    public async Task Cancellation_propagates_to_validator_and_skips_handler()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, CancellationValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sender.SendCommandAsync(new DeleteCommand("cancelled"), cancellationTokenSource.Token));

            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Validator_exception_propagates_and_skips_handler()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, ThrowingValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendCommandAsync(new DeleteCommand("throws"), CancellationToken.None));

            Assert.Equal("validator failed", exception.Message);
            Assert.Equal(0, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Validators_match_exact_runtime_types_only()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DerivedCommand, DerivedHandler>()
                .AddSliceForgeValidator<BaseCommand, FailingBaseValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DerivedCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public async Task Direct_fluent_validator_registration_is_ignored()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddTransient<IValidator<DeleteCommand>, FailingDeleteValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("direct"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, scope.ServiceProvider.GetRequiredService<TestState>().HandlerCount);
        }
    }

    [Fact]
    public void Duplicate_validator_registration_is_rejected()
    {
        ServiceCollection services = CreateBaseServices();

        services.AddSliceForgeValidator<DeleteCommand, PassingDeleteValidator>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidator<DeleteCommand, PassingDeleteValidator>());

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_sender_registration_is_rejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSliceForgeValidation());

        Assert.Contains("AddSliceForgeRuntime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ambiguous_sender_registration_is_rejected()
    {
        ServiceCollection services = new();
        services.AddScoped<IMessageSender, FirstSender>();
        services.AddScoped<IMessageSender, SecondSender>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidation());

        Assert.Contains("exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_sender_lifetime_is_rejected()
    {
        ServiceCollection services = new();
        services.AddTransient<IMessageSender, FirstSender>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidation());

        Assert.Contains("scoped", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_validation_enablement_is_rejected()
    {
        ServiceCollection services = CreateBaseServices();
        services.AddSliceForgeValidation();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidation());

        Assert.Contains("already enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scoped_validator_dependencies_are_resolved_from_current_scope()
    {
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddScoped<ScopedDependency>()
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, ScopedDependencyValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            ScopedDependency dependency = scope.ServiceProvider.GetRequiredService<ScopedDependency>();
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.SendCommandAsync(new DeleteCommand("scoped"), CancellationToken.None);

            Assert.Same(
                dependency,
                scope.ServiceProvider.GetRequiredService<TestState>().ValidatorDependency);
        }
    }

    [Fact]
    public async Task Message_and_cancellation_token_identity_are_preserved()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        DeleteCommand command = new("identity");
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, DeleteHandler>()
                .AddSliceForgeValidator<DeleteCommand, IdentityValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.SendCommandAsync(command, cancellationTokenSource.Token);

            TestState state = scope.ServiceProvider.GetRequiredService<TestState>();
            Assert.Same(command, state.LastValidatedMessage);
            Assert.Same(command, state.LastHandledMessage);
            Assert.Equal(cancellationTokenSource.Token, state.ReceivedToken);
        }
    }

    [Fact]
    public async Task Inner_failure_result_is_preserved_without_conversion()
    {
        Error error = new("users.failed", "The user could not be saved.", ErrorType.Failure);
        FailingHandler.Error = error;
        ServiceProvider provider = CreateProvider(services =>
            services
                .AddSliceForgeCommandHandler<DeleteCommand, FailingHandler>()
                .AddSliceForgeValidator<DeleteCommand, PassingDeleteValidator>()
                .AddSliceForgeValidation());

        using (provider)
        using (IServiceScope scope = provider.CreateScope())
        {
            IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Result result = await sender.SendCommandAsync(new DeleteCommand("failure"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Same(error, result.Error);
        }
    }

    private static ServiceProvider CreateProvider(Action<ServiceCollection> configure)
    {
        ServiceCollection services = CreateBaseServices();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static ServiceCollection CreateBaseServices()
    {
        ServiceCollection services = new();
        services.AddScoped<TestState>();
        services.AddSliceForgeRuntime();
        return services;
    }

    private static void AssertValidationFailure(
        Result result,
        string propertyName,
        params string[] messages)
    {
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("Validation.Failed", result.Error.Code);
        Assert.Equal("One or more validation errors occurred.", result.Error.Description);
        Assert.Equal(messages, result.Error.ValidationErrors[propertyName]);
    }
}

public sealed record DeleteCommand(string Value) : ICommand;

public sealed record CreateCommand(string Value) : ICommand<Guid>;

public sealed record UserQuery(string Value) : IQuery<string>;

public class BaseCommand : ICommand;

public sealed class DerivedCommand : BaseCommand;

public sealed class TestState
{
    public int HandlerCount { get; set; }

    public List<string> ValidatorOrder { get; } = new();

    public object? LastValidatedMessage { get; set; }

    public object? LastHandledMessage { get; set; }

    public CancellationToken ReceivedToken { get; set; }

    public ScopedDependency? ValidatorDependency { get; set; }
}

public sealed class ScopedDependency;

public sealed class DeleteHandler(TestState state) : ICommandHandler<DeleteCommand>
{
    public Task<Result> HandleAsync(DeleteCommand command, CancellationToken cancellationToken)
    {
        state.HandlerCount++;
        state.LastHandledMessage = command;
        state.ReceivedToken = cancellationToken;
        return Task.FromResult(Result.Success());
    }
}

public sealed class CreateHandler(TestState state) : ICommandHandler<CreateCommand, Guid>
{
    public static Guid Expected { get; set; }

    public Task<Result<Guid>> HandleAsync(CreateCommand command, CancellationToken cancellationToken)
    {
        state.HandlerCount++;
        return Task.FromResult(Result<Guid>.Success(Expected));
    }
}

public sealed class UserQueryHandler(TestState state) : IQueryHandler<UserQuery, string>
{
    public Task<Result<string>> HandleAsync(UserQuery query, CancellationToken cancellationToken)
    {
        state.HandlerCount++;
        return Task.FromResult(Result<string>.Success("Ada"));
    }
}

public sealed class FailingHandler(TestState state) : ICommandHandler<DeleteCommand>
{
    public static Error Error { get; set; } = new("default", "default", ErrorType.Failure);

    public Task<Result> HandleAsync(DeleteCommand command, CancellationToken cancellationToken)
    {
        state.HandlerCount++;
        return Task.FromResult(Result.Failure(Error));
    }
}

public sealed class DerivedHandler(TestState state) : ICommandHandler<DerivedCommand>
{
    public Task<Result> HandleAsync(DerivedCommand command, CancellationToken cancellationToken)
    {
        state.HandlerCount++;
        return Task.FromResult(Result.Success());
    }
}

public class FirstSender : IMessageSender
{
    public Task<Result> SendCommandAsync(ICommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());

    public Task<Result<TResponse>> SendCommandAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<TResponse>.Success(default!));

    public Task<Result<TResponse>> SendQueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<TResponse>.Success(default!));
}

public sealed class SecondSender : FirstSender;

public sealed class PassingDeleteValidator : AbstractValidator<DeleteCommand>
{
    public PassingDeleteValidator(TestState state)
        : base()
    {
        RuleFor(command => command.Value).Must((command, _) =>
        {
            state.LastValidatedMessage = command;
            return true;
        });
    }
}

public sealed class PassingCreateValidator : AbstractValidator<CreateCommand>
{
    public PassingCreateValidator() => RuleFor(command => command.Value).NotEmpty();
}

public sealed class PassingUserQueryValidator : AbstractValidator<UserQuery>
{
    public PassingUserQueryValidator() => RuleFor(query => query.Value).NotEmpty();
}

public sealed class FailingDeleteValidator : AbstractValidator<DeleteCommand>
{
    public FailingDeleteValidator() =>
        RuleFor(command => command.Value).Must(_ => false).WithMessage("The value is invalid.");
}

public sealed class FailingCreateValidator : AbstractValidator<CreateCommand>
{
    public FailingCreateValidator() =>
        RuleFor(command => command.Value).Must(_ => false).WithMessage("The create value is invalid.");
}

public sealed class FailingUserQueryValidator : AbstractValidator<UserQuery>
{
    public FailingUserQueryValidator() =>
        RuleFor(query => query.Value).Must(_ => false).WithMessage("The query value is invalid.");
}

public sealed class FirstPassingValidator : AbstractValidator<DeleteCommand>
{
    public FirstPassingValidator(TestState state)
    {
        RuleFor(command => command.Value).Must((_, _) =>
        {
            state.ValidatorOrder.Add("first");
            return true;
        });
    }
}

public sealed class SecondPassingValidator : AbstractValidator<DeleteCommand>
{
    public SecondPassingValidator(TestState state)
    {
        RuleFor(command => command.Value).Must((_, _) =>
        {
            state.ValidatorOrder.Add("second");
            return true;
        });
    }
}

public sealed class FirstFailingValidator : AbstractValidator<DeleteCommand>
{
    public FirstFailingValidator() =>
        RuleFor(command => command.Value).Must(_ => false).WithMessage("First failure.");
}

public sealed class SecondFailingValidator : AbstractValidator<DeleteCommand>
{
    public SecondFailingValidator()
    {
        RuleFor(command => command.Value).Must(_ => false).WithMessage("First failure.");
        RuleFor(command => command.Value).Must(_ => false).WithMessage("Second failure.");
    }
}

public sealed class ObjectLevelValidator : AbstractValidator<DeleteCommand>
{
    public ObjectLevelValidator()
    {
        RuleFor(command => command).Custom((_, context) =>
            context.AddFailure(string.Empty, "Object-level failure."));
    }
}

public sealed class CancellationValidator : AbstractValidator<DeleteCommand>
{
    public CancellationValidator(TestState state)
    {
        RuleFor(command => command.Value).MustAsync((_, _, cancellationToken) =>
        {
            state.ReceivedToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        });
    }
}

public sealed class ThrowingValidator : AbstractValidator<DeleteCommand>
{
    public ThrowingValidator() =>
        RuleFor(command => command.Value).Must(_ => throw new InvalidOperationException("validator failed"));
}

public sealed class FailingBaseValidator : AbstractValidator<BaseCommand>
{
    public FailingBaseValidator() => RuleFor(_ => _).Must(_ => false).WithMessage("base failure");
}

public sealed class ScopedDependencyValidator : AbstractValidator<DeleteCommand>
{
    public ScopedDependencyValidator(ScopedDependency dependency, TestState state)
    {
        state.ValidatorDependency = dependency;
        RuleFor(command => command.Value).NotEmpty();
    }
}

public sealed class IdentityValidator : AbstractValidator<DeleteCommand>
{
    public IdentityValidator(TestState state)
    {
        RuleFor(command => command.Value).MustAsync((command, _, cancellationToken) =>
        {
            state.LastValidatedMessage = command;
            state.ReceivedToken = cancellationToken;
            return Task.FromResult(true);
        });
    }
}
