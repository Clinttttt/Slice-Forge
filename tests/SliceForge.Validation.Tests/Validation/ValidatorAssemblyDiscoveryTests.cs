using System.Reflection;
using System.Runtime.Loader;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Runtime.Messaging;
using SliceForge.Validation.DependencyInjection;
using SliceForge.Validation.DiscoveryFixtures;

namespace SliceForge.Validation.Tests.Validation;

public sealed class ValidatorAssemblyDiscoveryTests
{
    [Fact]
    public async Task Discovers_internal_validator_and_short_circuits_invalid_command()
    {
        ValidatorDiscoveryProbe probe = new();
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = new();
        services.AddSingleton(probe);
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeRuntime();
        services.AddSliceForgeCommandHandler<InternalValidatedCommand, InternalCommandHandler>();
        Assert.Same(
            services,
            services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly));
        services.AddSliceForgeValidation();

        ServiceDescriptor publicValidatorDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(PublicValidatedCommandValidator));
        Assert.Equal(ServiceLifetime.Transient, publicValidatorDescriptor.Lifetime);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        Assert.NotSame(
            scope.ServiceProvider.GetRequiredService<PublicValidatedCommandValidator>(),
            scope.ServiceProvider.GetRequiredService<PublicValidatedCommandValidator>());
        Result validResult = await sender.SendCommandAsync(
            new InternalValidatedCommand("valid"),
            CancellationToken.None);
        Result invalidResult = await sender.SendCommandAsync(
            new InternalValidatedCommand(string.Empty),
            CancellationToken.None);

        Assert.True(validResult.IsSuccess);
        Assert.False(invalidResult.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidResult.Error?.Type);
        Assert.Equal(1, handlerCounter.CallCount);
        Assert.Equal(2, probe.ValidationCount);
    }

    [Fact]
    public async Task Discovers_public_typed_command_and_query_validators()
    {
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices();
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<PublicValidatedCommand, Guid, PublicCommandHandler>();
        services.AddSliceForgeQueryHandler<QueryValidatedMessage, string, QueryHandler>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result<Guid> validCommand = await sender.SendCommandAsync<Guid>(
            new PublicValidatedCommand("valid"),
            CancellationToken.None);
        Result<Guid> invalidCommand = await sender.SendCommandAsync<Guid>(
            new PublicValidatedCommand(string.Empty),
            CancellationToken.None);
        Result<string> validQuery = await sender.SendQueryAsync(
            new QueryValidatedMessage("valid"),
            CancellationToken.None);
        Result<string> invalidQuery = await sender.SendQueryAsync(
            new QueryValidatedMessage("invalid"),
            CancellationToken.None);

        Assert.True(validCommand.IsSuccess);
        Assert.False(invalidCommand.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidCommand.Error?.Type);
        Assert.True(validQuery.IsSuccess);
        Assert.Equal("query response", validQuery.Value);
        Assert.False(invalidQuery.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidQuery.Error?.Type);
        Assert.Equal(2, handlerCounter.CallCount);
    }

    [Fact]
    public async Task Discovers_a_validator_that_implements_IValidator_directly()
    {
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices();
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            DirectInterfaceValidatedCommand,
            CountingCommandHandler<DirectInterfaceValidatedCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result result = await sender.SendCommandAsync(
            new DirectInterfaceValidatedCommand(string.Empty),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Equal(0, handlerCounter.CallCount);
    }

    [Fact]
    public async Task Discovered_validators_run_in_stable_order_and_aggregate_deduplicated_failures()
    {
        ValidatorDiscoveryProbe probe = new();
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices(probe);
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            OrderedValidatedCommand,
            CountingCommandHandler<OrderedValidatedCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result result = await sender.SendCommandAsync(
            new OrderedValidatedCommand("invalid"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Equal("Validation.Failed", result.Error?.Code);
        Assert.Equal(["Alpha", "Zeta"], probe.OrderedCalls);
        Assert.Equal(["Shared failure.", "Zeta failure."], result.Error?.ValidationErrors["Value"]);
        Assert.Equal(0, handlerCounter.CallCount);
    }

    [Fact]
    public async Task Explicit_validator_registered_before_discovery_runs_first()
    {
        ValidatorDiscoveryProbe probe = new();
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices(probe);
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            OrderedValidatedCommand,
            CountingCommandHandler<OrderedValidatedCommand>>();
        services.AddSliceForgeValidator<OrderedValidatedCommand, ManualOrderedValidator>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        await sender.SendCommandAsync(new OrderedValidatedCommand("valid"), CancellationToken.None);

        Assert.Equal(["Manual", "Alpha", "Zeta"], probe.OrderedCalls);
    }

    [Fact]
    public async Task Explicit_validator_registered_after_discovery_runs_last()
    {
        ValidatorDiscoveryProbe probe = new();
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices(probe);
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            OrderedValidatedCommand,
            CountingCommandHandler<OrderedValidatedCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidator<OrderedValidatedCommand, ManualOrderedValidator>();
        services.AddSliceForgeValidation();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        await sender.SendCommandAsync(new OrderedValidatedCommand("valid"), CancellationToken.None);

        Assert.Equal(["Alpha", "Zeta", "Manual"], probe.OrderedCalls);
    }

    [Fact]
    public async Task Discovery_after_validation_enablement_is_visible_when_the_provider_is_built()
    {
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices();
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<PublicValidatedCommand, Guid, PublicCommandHandler>();
        services.AddSliceForgeValidation();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result<Guid> result = await sender.SendCommandAsync<Guid>(
            new PublicValidatedCommand(string.Empty),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Equal(0, handlerCounter.CallCount);
    }

    [Fact]
    public async Task One_validator_can_validate_multiple_distinct_message_types()
    {
        ValidatorDiscoveryProbe probe = new();
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices(probe);
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            FirstMultiContractCommand,
            CountingCommandHandler<FirstMultiContractCommand>>();
        services.AddSliceForgeCommandHandler<
            SecondMultiContractCommand,
            CountingCommandHandler<SecondMultiContractCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        Assert.Single(services, descriptor => descriptor.ImplementationType?.Name == "MultiContractValidator");

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result first = await sender.SendCommandAsync(new FirstMultiContractCommand("one"), CancellationToken.None);
        Result second = await sender.SendCommandAsync(new SecondMultiContractCommand("two"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(["first", "second"], probe.MultiContractCalls);
        Assert.Equal(2, handlerCounter.CallCount);
    }

    [Fact]
    public async Task Discovered_validator_dependencies_resolve_from_each_active_scope()
    {
        ValidatorDiscoveryProbe probe = new();
        ServiceCollection services = CreateBaseServices(probe);
        services.AddSingleton(new HandlerCallCounter());
        services.AddScoped<ScopedValidatorDependency>();
        services.AddSliceForgeCommandHandler<
            DependencyValidatedCommand,
            CountingCommandHandler<DependencyValidatedCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        ScopedValidatorDependency firstDependency;
        using (IServiceScope firstScope = provider.CreateScope())
        {
            firstDependency = firstScope.ServiceProvider.GetRequiredService<ScopedValidatorDependency>();
            IMessageSender sender = firstScope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.SendCommandAsync(new DependencyValidatedCommand("first"), CancellationToken.None);
            Assert.Same(firstDependency, probe.LastScopedDependency);
        }

        using (IServiceScope secondScope = provider.CreateScope())
        {
            ScopedValidatorDependency secondDependency =
                secondScope.ServiceProvider.GetRequiredService<ScopedValidatorDependency>();
            IMessageSender sender = secondScope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.SendCommandAsync(new DependencyValidatedCommand("second"), CancellationToken.None);

            Assert.NotSame(firstDependency, secondDependency);
            Assert.Same(secondDependency, probe.LastScopedDependency);
        }
    }

    [Fact]
    public async Task Abstract_open_generic_and_unrelated_types_are_not_discovered()
    {
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices();
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            AbstractValidatorTargetCommand,
            CountingCommandHandler<AbstractValidatorTargetCommand>>();
        services.AddSliceForgeCommandHandler<
            GenericValidatedCommand<string>,
            CountingCommandHandler<GenericValidatedCommand<string>>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(UnrelatedFixtureType));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result abstractTarget = await sender.SendCommandAsync(
            new AbstractValidatorTargetCommand("accepted"),
            CancellationToken.None);
        Result openGenericTarget = await sender.SendCommandAsync(
            new GenericValidatedCommand<string>("accepted"),
            CancellationToken.None);

        Assert.True(abstractTarget.IsSuccess);
        Assert.True(openGenericTarget.IsSuccess);
        Assert.Equal(2, handlerCounter.CallCount);
    }

    [Fact]
    public async Task Validator_discovery_does_not_register_handlers_from_the_supplied_assembly()
    {
        ServiceCollection services = CreateBaseServices();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new UnvalidatedDiscoveryCommand(), CancellationToken.None));

        Assert.Contains("No message route", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validator_types_in_another_assembly_are_not_discovered()
    {
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices();
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            OutsideAssemblyCommand,
            CountingCommandHandler<OutsideAssemblyCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result result = await sender.SendCommandAsync(new OutsideAssemblyCommand("invalid"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handlerCounter.CallCount);
    }

    [Fact]
    public async Task Validator_routes_match_exact_concrete_runtime_types()
    {
        HandlerCallCounter handlerCounter = new();
        ServiceCollection services = CreateBaseServices();
        services.AddSingleton(handlerCounter);
        services.AddSliceForgeCommandHandler<
            ValidationDerivedCommand,
            CountingCommandHandler<ValidationDerivedCommand>>();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        services.AddSliceForgeValidation();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result result = await sender.SendCommandAsync(
            new ValidationDerivedCommand("derived"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, handlerCounter.CallCount);
    }

    [Fact]
    public void Manual_then_discovered_duplicate_is_rejected_without_partial_registration()
    {
        ServiceCollection services = CreateBaseServices();
        services.AddSliceForgeValidator<PublicValidatedCommand, PublicValidatedCommandValidator>();
        int descriptorCount = services.Count;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly));

        Assert.Contains(nameof(PublicValidatedCommandValidator), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PublicValidatedCommand), exception.Message, StringComparison.Ordinal);
        Assert.Equal(descriptorCount, services.Count);
    }

    [Fact]
    public void Discovered_then_manual_duplicate_is_rejected()
    {
        ServiceCollection services = CreateBaseServices();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidator<PublicValidatedCommand, PublicValidatedCommandValidator>());

        Assert.Contains(nameof(PublicValidatedCommandValidator), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PublicValidatedCommand), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_assembly_registration_is_rejected_without_partial_registration()
    {
        ServiceCollection services = CreateBaseServices();
        services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly);
        int descriptorCount = services.Count;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeValidatorsFromAssembly(typeof(InternalValidatedCommand).Assembly));

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Validator '", exception.Message, StringComparison.Ordinal);
        Assert.Contains("for message '", exception.Message, StringComparison.Ordinal);
        Assert.Equal(descriptorCount, services.Count);
    }

    [Fact]
    public void Empty_assembly_is_a_no_op_and_null_arguments_are_rejected()
    {
        ServiceCollection services = new();
        int descriptorCount = services.Count;

        Assert.Same(services, services.AddSliceForgeValidatorsFromAssembly(typeof(Result).Assembly));
        Assert.Equal(descriptorCount, services.Count);
        Assert.Throws<ArgumentNullException>(() =>
            SliceForge.Validation.DependencyInjection.ServiceCollectionExtensions
                .AddSliceForgeValidatorsFromAssembly(null!, typeof(Result).Assembly));
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddSliceForgeValidatorsFromAssembly(null!));
    }

    [Fact]
    public void Incomplete_type_loading_reports_loader_details_without_mutating_services()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "SliceForge.Runtime.TypeLoadFixtures.dll");
        string coreAssemblyName = typeof(ICommand).Assembly.GetName().Name!;
        const string missingAssemblyName = "SliceForge.Runtime.TypeLoadDependency";
        MissingDependencyLoadContext loadContext = new(coreAssemblyName, missingAssemblyName);

        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(fixturePath);
            ServiceCollection services = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddSliceForgeValidatorsFromAssembly(assembly));

            ReflectionTypeLoadException typeLoadException =
                Assert.IsType<ReflectionTypeLoadException>(exception.InnerException);
            Assert.Contains(missingAssemblyName, exception.Message, StringComparison.Ordinal);
            Assert.Contains(
                typeLoadException.LoaderExceptions,
                loaderException => loaderException is FileNotFoundException);
            Assert.Empty(services);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private sealed class HandlerCallCounter
    {
        public int CallCount { get; set; }
    }

    private sealed class InternalCommandHandler(HandlerCallCounter counter)
        : ICommandHandler<InternalValidatedCommand>
    {

        public Task<Result> HandleAsync(
            InternalValidatedCommand command,
            CancellationToken cancellationToken)
        {
            counter.CallCount++;
            return Task.FromResult(Result.Success());
        }
    }

    private static ServiceCollection CreateBaseServices(ValidatorDiscoveryProbe? probe = null)
    {
        ServiceCollection services = new();
        services.AddSingleton(probe ?? new ValidatorDiscoveryProbe());
        services.AddSliceForgeRuntime();
        return services;
    }

    private sealed class CountingCommandHandler<TCommand>(HandlerCallCounter counter)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken)
        {
            counter.CallCount++;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class PublicCommandHandler(HandlerCallCounter counter)
        : ICommandHandler<PublicValidatedCommand, Guid>
    {
        public Task<Result<Guid>> HandleAsync(
            PublicValidatedCommand command,
            CancellationToken cancellationToken)
        {
            counter.CallCount++;
            return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        }
    }

    private sealed class QueryHandler(HandlerCallCounter counter)
        : IQueryHandler<QueryValidatedMessage, string>
    {
        public Task<Result<string>> HandleAsync(
            QueryValidatedMessage query,
            CancellationToken cancellationToken)
        {
            counter.CallCount++;
            return Task.FromResult(Result<string>.Success("query response"));
        }
    }

    private sealed class ManualOrderedValidator : AbstractValidator<OrderedValidatedCommand>
    {
        public ManualOrderedValidator(ValidatorDiscoveryProbe probe) : base() =>
            RuleFor(command => command.Value).Must((_, _) =>
            {
                probe.OrderedCalls.Add("Manual");
                return true;
            });
    }

    private sealed record OutsideAssemblyCommand(string Value) : ICommand;

    private sealed class OutsideAssemblyValidator : AbstractValidator<OutsideAssemblyCommand>
    {
        public OutsideAssemblyValidator() =>
            RuleFor(command => command.Value).Must(_ => false).WithMessage("Outside validator ran.");
    }

    private sealed class MissingDependencyLoadContext(
        string sharedAssemblyName,
        string missingAssemblyName) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == sharedAssemblyName)
            {
                return typeof(ICommand).Assembly;
            }

            if (assemblyName.Name == missingAssemblyName)
            {
                throw new FileNotFoundException($"Test intentionally hides '{missingAssemblyName}'.");
            }

            return null;
        }
    }
}
