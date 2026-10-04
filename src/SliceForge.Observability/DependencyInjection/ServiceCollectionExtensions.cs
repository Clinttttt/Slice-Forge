using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SliceForge.Observability.Messaging;
using SliceForge.Runtime.Messaging;

namespace SliceForge.Observability.DependencyInjection;

/// <summary>Registers message execution instrumentation around the scoped Runtime sender.</summary>
public static class ServiceCollectionExtensions
{
    private static readonly object CapturedSenderKey = new();

    /// <summary>
    /// Decorates the existing scoped message sender with logging, activities, and metrics.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sender is missing, ambiguous, keyed, unsupported, or already decorated.
    /// </exception>
    public static IServiceCollection AddSliceForgeObservability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ObservabilityEnabledMarker)))
        {
            throw new InvalidOperationException("SliceForge observability is already enabled.");
        }

        ServiceDescriptor[] senderDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IMessageSender))
            .ToArray();

        if (senderDescriptors.Length == 0)
        {
            throw new InvalidOperationException(
                "SliceForge observability requires exactly one registered IMessageSender. " +
                "Call AddSliceForgeRuntime before AddSliceForgeObservability.");
        }

        if (senderDescriptors.Any(descriptor => descriptor.IsKeyedService))
        {
            throw new InvalidOperationException(
                "SliceForge observability does not support keyed IMessageSender registrations.");
        }

        if (senderDescriptors.Length != 1)
        {
            throw new InvalidOperationException(
                "SliceForge observability requires exactly one registered IMessageSender.");
        }

        ServiceDescriptor senderDescriptor = senderDescriptors[0];

        if (senderDescriptor.ImplementationInstance is not null)
        {
            throw new InvalidOperationException(
                "SliceForge observability does not support IMessageSender implementation instances.");
        }

        if (senderDescriptor.Lifetime != ServiceLifetime.Scoped)
        {
            throw new InvalidOperationException(
                "SliceForge observability requires IMessageSender to use a scoped lifetime.");
        }

        ServiceDescriptor keyedSenderDescriptor = CreateKeyedSenderDescriptor(senderDescriptor);

        services.Remove(senderDescriptor);
        services.Add(keyedSenderDescriptor);
        services.AddScoped<IMessageSender>(serviceProvider =>
        {
            IMessageSender innerSender = serviceProvider.GetRequiredKeyedService<IMessageSender>(CapturedSenderKey);
            ILogger logger = CreateLogger(serviceProvider);

            return new ObservabilityMessageSender(innerSender, logger);
        });
        services.AddSingleton<ObservabilityEnabledMarker>();

        return services;
    }

    private static ServiceDescriptor CreateKeyedSenderDescriptor(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType is Type implementationType)
        {
            return ServiceDescriptor.DescribeKeyed(
                typeof(IMessageSender),
                CapturedSenderKey,
                implementationType,
                ServiceLifetime.Scoped);
        }

        if (descriptor.ImplementationFactory is { } implementationFactory)
        {
            return ServiceDescriptor.DescribeKeyed(
                typeof(IMessageSender),
                CapturedSenderKey,
                (serviceProvider, _) => implementationFactory(serviceProvider)!,
                ServiceLifetime.Scoped);
        }

        throw new InvalidOperationException(
            "SliceForge observability requires an IMessageSender implementation type or factory.");
    }

    private static ILogger CreateLogger(IServiceProvider serviceProvider)
    {
        try
        {
            return serviceProvider.GetService<ILoggerFactory>()?
                .CreateLogger(SliceForgeInstrumentation.LoggerCategoryName) ?? NullLogger.Instance;
        }
        catch (Exception)
        {
            return NullLogger.Instance;
        }
    }

    private sealed class ObservabilityEnabledMarker;
}
