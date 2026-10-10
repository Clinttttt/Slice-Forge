using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SliceForge.Runtime.Messaging;
using SliceForge.Validation.Messaging;
using SliceForge.Validation.Routing;

namespace SliceForge.Validation.DependencyInjection;

/// <summary>
/// Registers explicit or assembly-discovered validation routes and the validation sender decorator.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Discovers closed FluentValidation validators in the supplied assembly and registers exact message routes.
    /// </summary>
    /// <remarks>
    /// Only the supplied assembly is inspected. Concrete, closed validator types are ordered by fully qualified type
    /// name for each message. Validators are added after any earlier explicit registrations; later explicit
    /// registrations remain last. Abstract, interface, and open generic types are ignored.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assembly">The single consumer assembly to inspect.</param>
    /// <returns>The same service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="assembly"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The assembly cannot be inspected, or a validator/message registration is duplicated.
    /// </exception>
    public static IServiceCollection AddSliceForgeValidatorsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        IReadOnlyList<DiscoveredValidatorRegistration> registrations =
            ValidatorAssemblyScanner.Discover(assembly);
        if (registrations.Count == 0)
        {
            return services;
        }

        ValidationRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);
        registrationStore.AddRange(registrations.Select(registration => registration.Route).ToArray());

        foreach (Type validatorType in registrations
                     .Select(registration => registration.ValidatorType)
                     .Distinct())
        {
            services.TryAdd(ServiceDescriptor.Transient(validatorType, validatorType));
        }

        return services;
    }

    /// <summary>
    /// Registers one validator for one exact concrete message type.
    /// </summary>
    /// <typeparam name="TMessage">The concrete command or query message type.</typeparam>
    /// <typeparam name="TValidator">The validator type for the message.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the same validator type is registered twice for the same message.
    /// </exception>
    public static IServiceCollection AddSliceForgeValidator<TMessage, TValidator>(
        this IServiceCollection services)
        where TValidator : class, IValidator<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);

        ValidationRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);
        registrationStore.Add<TMessage, TValidator>();
        services.TryAddTransient<TValidator>();

        return services;
    }

    /// <summary>
    /// Decorates the existing scoped message sender with exact-type validation.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Runtime sender is absent, ambiguous, unsupported, or already decorated.
    /// </exception>
    public static IServiceCollection AddSliceForgeValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ValidationEnabledMarker)))
        {
            throw new InvalidOperationException("SliceForge validation is already enabled.");
        }

        ServiceDescriptor[] senderDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IMessageSender))
            .ToArray();

        if (senderDescriptors.Length == 0)
        {
            throw new InvalidOperationException(
                "SliceForge validation requires exactly one registered IMessageSender. " +
                "Call AddSliceForgeRuntime before AddSliceForgeValidation.");
        }

        if (senderDescriptors.Length != 1)
        {
            throw new InvalidOperationException(
                "SliceForge validation requires exactly one registered IMessageSender.");
        }

        ServiceDescriptor senderDescriptor = senderDescriptors[0];

        if (senderDescriptor.Lifetime != ServiceLifetime.Scoped ||
            senderDescriptor.ImplementationInstance is not null)
        {
            throw new InvalidOperationException(
                "SliceForge validation requires IMessageSender to use a supported scoped registration.");
        }

        ValidationRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(ValidationRouteCatalog)))
        {
            services.AddSingleton(_ => new ValidationRouteCatalog(registrationStore.Snapshot()));
        }

        services.Remove(senderDescriptor);
        services.AddScoped<IMessageSender>(serviceProvider =>
        {
            IMessageSender innerSender = ResolveCapturedSender(senderDescriptor, serviceProvider);
            ValidationRouteCatalog routeCatalog =
                serviceProvider.GetRequiredService<ValidationRouteCatalog>();

            return new ValidationMessageSender(innerSender, routeCatalog, serviceProvider);
        });
        services.AddSingleton<ValidationEnabledMarker>();

        return services;
    }

    private static ValidationRegistrationStore GetOrCreateRegistrationStore(
        IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(ValidationRegistrationStore) &&
                descriptor.ImplementationInstance is ValidationRegistrationStore registrationStore)
            {
                return registrationStore;
            }
        }

        ValidationRegistrationStore newRegistrationStore = new();
        services.AddSingleton(newRegistrationStore);
        return newRegistrationStore;
    }

    private static IMessageSender ResolveCapturedSender(
        ServiceDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        object? sender = descriptor.ImplementationFactory is not null
            ? descriptor.ImplementationFactory(serviceProvider)
            : descriptor.ImplementationType is not null
                ? ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType)
                : null;

        return sender as IMessageSender ??
            throw new InvalidOperationException(
                "The captured IMessageSender registration did not produce an IMessageSender.");
    }

    private sealed class ValidationEnabledMarker;
}
