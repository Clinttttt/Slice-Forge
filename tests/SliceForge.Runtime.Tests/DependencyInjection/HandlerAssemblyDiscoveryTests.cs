using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Runtime.DiscoveryFixtures;
using SliceForge.Runtime.DuplicateFixtures;
using SliceForge.Runtime.Messaging;

namespace SliceForge.Runtime.Tests.DependencyInjection;

public sealed class HandlerAssemblyDiscoveryTests
{
    [Fact]
    public async Task Discovers_internal_non_generic_command_handler_and_preserves_result_and_token()
    {
        Result expectedResult = Result.Failure(
            new Error("fixture.failure", "Expected fixture failure.", ErrorType.Failure));
        using ServiceProvider provider = CreateProvider(services =>
        {
            DiscoveryProbe probe = new() { NonGenericResult = expectedResult };
            services.AddSingleton(probe);
            Assert.Same(services, services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly));
        });
        using IServiceScope scope = provider.CreateScope();
        using CancellationTokenSource cancellationSource = new();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result actual = await sender.SendCommandAsync(new DiscoveredDeleteCommand(), cancellationSource.Token);

        Assert.Same(expectedResult, actual);
        DiscoveryProbe resolvedProbe = scope.ServiceProvider.GetRequiredService<DiscoveryProbe>();
        Assert.Equal(1, resolvedProbe.NonGenericCalls);
        Assert.Equal(cancellationSource.Token, resolvedProbe.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Discovers_typed_command_handler_and_preserves_result_identity()
    {
        Result<Guid> expectedResult = Result<Guid>.Failure(
            new Error("fixture.typed-failure", "Expected typed fixture failure.", ErrorType.Conflict));
        DiscoveryProbe probe = new() { TypedResult = expectedResult };
        using ServiceProvider provider = CreateProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly);
        });
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result<Guid> result = await sender.SendCommandAsync<Guid>(
            new DiscoveredCreateCommand(),
            CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.Equal(1, probe.TypedCalls);
    }

    [Fact]
    public async Task Discovers_query_handler_and_propagates_response_and_cancellation()
    {
        DiscoveryProbe probe = new();
        using ServiceProvider provider = CreateProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly);
        });
        using IServiceScope scope = provider.CreateScope();
        using CancellationTokenSource cancellationSource = new();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result<string> result = await sender.SendQueryAsync(
            new DiscoveredEchoQuery("discovered response"),
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("discovered response", result.Value);
        Assert.Equal(1, probe.QueryCalls);
        Assert.Equal(cancellationSource.Token, probe.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Discovers_all_distinct_contracts_implemented_by_one_handler()
    {
        DiscoveryProbe probe = new();
        using ServiceProvider provider = CreateProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly);
        });
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result first = await sender.SendCommandAsync(new FirstMultiContractCommand(), CancellationToken.None);
        Result second = await sender.SendCommandAsync(new SecondMultiContractCommand(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(1, probe.FirstMultiContractCalls);
        Assert.Equal(1, probe.SecondMultiContractCalls);
    }

    [Fact]
    public async Task Ignores_abstract_open_generic_and_unrelated_types()
    {
        using ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly));
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new AbstractOnlyCommand(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new OpenGenericCommand<string>("open"), CancellationToken.None));
    }

    [Fact]
    public async Task Routes_discovered_messages_by_exact_runtime_type()
    {
        using ServiceProvider provider = CreateProvider(services =>
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly));
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new DiscoveredDerivedCommand(), CancellationToken.None));
    }

    [Fact]
    public void Empty_assembly_is_a_no_op()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        int descriptorCount = services.Count;

        IServiceCollection returned = services.AddSliceForgeHandlersFromAssembly(typeof(Result).Assembly);

        Assert.Same(services, returned);
        Assert.Equal(descriptorCount, services.Count);
    }

    [Fact]
    public async Task Discovery_does_not_register_handlers_from_other_assemblies()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly);
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendCommandAsync(new AssemblyBoundaryCommand(), CancellationToken.None));

        Assert.Contains(nameof(AssemblyBoundaryCommand), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovered_command_duplicates_report_both_handlers_deterministically()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSliceForgeHandlersFromAssembly(typeof(DuplicateCommand).Assembly));
        InvalidOperationException repeatedException = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSliceForgeHandlersFromAssembly(typeof(DuplicateCommand).Assembly));

        Assert.Equal(exception.Message, repeatedException.Message);
        Assert.Contains(nameof(DuplicateCommand), exception.Message, StringComparison.Ordinal);
        Assert.Contains("FirstDuplicateCommandHandler", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SecondDuplicateCommandHandler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovered_query_duplicates_are_rejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSliceForgeHandlersFromAssembly(typeof(DuplicateQuery).Assembly));

        Assert.Contains(nameof(DuplicateQuery), exception.Message, StringComparison.Ordinal);
        Assert.Contains("FirstDuplicateQueryHandler", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SecondDuplicateQueryHandler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovered_command_query_route_collision_is_rejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSliceForgeHandlersFromAssembly(typeof(CommandQueryCollision).Assembly));

        Assert.Contains(nameof(CommandQueryCollision), exception.Message, StringComparison.Ordinal);
        Assert.Contains("CollisionCommandHandler", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CollisionQueryHandler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Conflicting_discovery_does_not_partially_mutate_service_collection()
    {
        ServiceCollection services = new();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeHandlersFromAssembly(typeof(DuplicateCommand).Assembly));

        Assert.Empty(services);
    }

    [Fact]
    public void Manual_registration_then_discovery_rejects_the_same_message_route()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeCommandHandler<DiscoveredDeleteCommand, ManualDiscoveredDeleteHandler>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly));

        Assert.Contains(nameof(DiscoveredDeleteCommand), exception.Message, StringComparison.Ordinal);
        Assert.Contains("DiscoveredDeleteHandler", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_then_manual_registration_rejects_the_same_message_route()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeCommandHandler<DiscoveredDeleteCommand, ManualDiscoveredDeleteHandler>());

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_assembly_registration_rejects_existing_routes()
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSliceForgeHandlersFromAssembly(typeof(DiscoveryProbe).Assembly));

        Assert.Contains("already has a registered route", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_service_collection_and_assembly_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddSliceForgeHandlersFromAssembly(null!, typeof(DiscoveryProbe).Assembly));
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddSliceForgeHandlersFromAssembly(null!));
    }

    [Fact]
    public void Incomplete_type_loading_throws_with_original_loader_details()
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
                services.AddSliceForgeHandlersFromAssembly(assembly));

            ReflectionTypeLoadException typeLoadException = Assert.IsType<ReflectionTypeLoadException>(
                exception.InnerException);
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

    private static ServiceProvider CreateProvider(Action<ServiceCollection> configure)
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        configure(services);
        return services.BuildServiceProvider();
    }
}

public sealed record AssemblyBoundaryCommand : ICommand;

public sealed record ManualDiscoveredDeleteHandler : ICommandHandler<DiscoveredDeleteCommand>
{
    public Task<Result> HandleAsync(DiscoveredDeleteCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

file sealed class MissingDependencyLoadContext(
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
