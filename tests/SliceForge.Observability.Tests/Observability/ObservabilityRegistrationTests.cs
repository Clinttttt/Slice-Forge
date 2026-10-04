using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.Observability.DependencyInjection;
using SliceForge.Observability.Tests.Support;
using SliceForge.Runtime.Messaging;

namespace SliceForge.Observability.Tests.Observability;

public sealed class ObservabilityRegistrationTests
{
    [Fact]
    public void Instrumentation_names_are_stable_public_subscription_points()
    {
        Assert.Equal("SliceForge.Observability", SliceForgeInstrumentation.ActivitySourceName);
        Assert.Equal("SliceForge.Observability", SliceForgeInstrumentation.MeterName);
        Assert.Equal("SliceForge.Observability", SliceForgeInstrumentation.LoggerCategoryName);
    }

    [Fact]
    public void Assembly_exposes_only_the_approved_public_observability_contract()
    {
        string[] exportedTypeNames = typeof(SliceForgeInstrumentation).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(
            new[]
            {
                "SliceForge.Observability.DependencyInjection.ServiceCollectionExtensions",
                "SliceForge.Observability.SliceForgeInstrumentation"
            },
            exportedTypeNames);

        string[] declaredPublicMethods = typeof(ServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(new[] { "AddSliceForgeObservability" }, declaredPublicMethods);
    }

    [Fact]
    public void Registration_rejects_a_missing_sender()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSliceForgeObservability());

        Assert.Contains("AddSliceForgeRuntime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_rejects_ambiguous_unkeyed_senders()
    {
        ServiceCollection services = new();
        services.AddScoped<IMessageSender, TestMessageSender>();
        services.AddScoped<IMessageSender, TestMessageSender>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSliceForgeObservability());

        Assert.Contains("exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_rejects_keyed_sender_input()
    {
        ServiceCollection services = new();
        services.AddKeyedScoped<IMessageSender, TestMessageSender>("external-key");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSliceForgeObservability());

        Assert.Contains("keyed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registration_rejects_unsupported_sender_lifetime()
    {
        ServiceCollection services = new();
        services.AddSingleton<IMessageSender, TestMessageSender>();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSliceForgeObservability());

        Assert.Contains("scoped", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registration_rejects_sender_implementation_instances()
    {
        ServiceCollection services = new();
        services.AddSingleton<IMessageSender>(new TestMessageSender());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSliceForgeObservability());

        Assert.Contains("instance", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registration_rejects_duplicate_enablement()
    {
        ServiceCollection services = new();
        services.AddScoped<IMessageSender, TestMessageSender>();
        services.AddSliceForgeObservability();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSliceForgeObservability());

        Assert.Contains("already enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registration_preserves_scoped_factory_and_disposal_ownership()
    {
        ServiceCollection services = new();
        ScopedSenderTracker tracker = new();
        services.AddScoped<IMessageSender>(_ => new DisposableTestMessageSender(tracker));
        services.AddSliceForgeObservability();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        IServiceScope scope = provider.CreateScope();
        IMessageSender first = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        IMessageSender second = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Assert.Same(first, second);
        Assert.Equal(1, tracker.CreatedCount);
        Assert.Equal(0, tracker.DisposedCount);

        scope.Dispose();

        Assert.Equal(1, tracker.DisposedCount);
    }

    [Fact]
    public void Registration_preserves_implementation_type_scoped_disposal()
    {
        ServiceCollection services = new();
        ScopedSenderTracker tracker = new();
        services.AddSingleton(tracker);
        services.AddScoped<IMessageSender, DisposableTestMessageSender>();
        services.AddSliceForgeObservability();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using (IServiceScope scope = provider.CreateScope())
        {
            _ = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            Assert.Equal(1, tracker.CreatedCount);
        }

        Assert.Equal(1, tracker.DisposedCount);
    }
}

internal class TestMessageSender : IMessageSender
{
    public Task<SliceForge.Results.Result> SendCommandAsync(
        SliceForge.Messaging.ICommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(SliceForge.Results.Result.Success());

    public Task<SliceForge.Results.Result<TResponse>> SendCommandAsync<TResponse>(
        SliceForge.Messaging.ICommand<TResponse> command,
        CancellationToken cancellationToken) =>
        Task.FromResult(SliceForge.Results.Result<TResponse>.Success(default!));

    public Task<SliceForge.Results.Result<TResponse>> SendQueryAsync<TResponse>(
        SliceForge.Messaging.IQuery<TResponse> query,
        CancellationToken cancellationToken) =>
        Task.FromResult(SliceForge.Results.Result<TResponse>.Success(default!));
}

internal sealed class ScopedSenderTracker
{
    public int CreatedCount { get; set; }

    public int DisposedCount { get; set; }
}

internal sealed class DisposableTestMessageSender : TestMessageSender, IDisposable
{
    private readonly ScopedSenderTracker _tracker;

    public DisposableTestMessageSender()
        : this(new ScopedSenderTracker())
    {
    }

    public DisposableTestMessageSender(ScopedSenderTracker tracker)
    {
        _tracker = tracker;
        tracker.CreatedCount++;
    }

    public void Dispose() => _tracker.DisposedCount++;
}
