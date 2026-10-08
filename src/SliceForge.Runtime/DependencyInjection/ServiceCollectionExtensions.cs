using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SliceForge.Messaging;
using SliceForge.Runtime.Messaging;
using SliceForge.Runtime.Routing;

namespace SliceForge.Runtime.DependencyInjection;

/// <summary>
/// Registers SliceForge Runtime execution services and explicit message routes.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the scoped message sender and immutable route catalog.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for further configuration.</returns>
    public static IServiceCollection AddSliceForgeRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        RouteRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(RouteCatalog)))
        {
            services.AddSingleton(_ => new RouteCatalog(registrationStore.Snapshot()));
        }

        services.TryAddScoped<IMessageSender, DefaultMessageSender>();

        return services;
    }

    /// <summary>
    /// Discovers supported command and query handlers in the supplied assembly and registers their exact routes.
    /// </summary>
    /// <remarks>
    /// Only the supplied assembly is inspected. Abstract, interface, and open generic types are ignored; duplicate
    /// message routes fail during registration. Assemblies with no supported handlers have no effect. Discovery does
    /// not include validators or endpoints.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assembly">The single consumer assembly to inspect.</param>
    /// <returns>The same service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="assembly"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The assembly contains conflicting routes or cannot be inspected.</exception>
    public static IServiceCollection AddSliceForgeHandlersFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        IReadOnlyList<DiscoveredHandlerRegistration> registrations = HandlerAssemblyScanner.Discover(assembly);
        if (registrations.Count == 0)
        {
            return services;
        }

        RouteRegistrationStore? existingStore = FindRegistrationStore(services);
        HandlerAssemblyScanner.ThrowIfConflictingRoutes(
            assembly,
            registrations,
            existingStore?.Snapshot() ?? []);

        RouteRegistrationStore registrationStore = existingStore ?? GetOrCreateRegistrationStore(services);
        foreach (DiscoveredHandlerRegistration registration in registrations)
        {
            HandlerRouteRegistration.Register(
                services,
                registrationStore,
                registration.Route,
                registration.HandlerContract,
                registration.HandlerType);
        }

        return services;
    }

    /// <summary>Registers one non-generic command handler and its exact route.</summary>
    public static IServiceCollection AddSliceForgeCommandHandler<TCommand, THandler>(
        this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        ArgumentNullException.ThrowIfNull(services);

        RouteRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);
        HandlerRouteRegistration.Register(
            services,
            registrationStore,
            new CommandRoute<TCommand>(),
            typeof(ICommandHandler<TCommand>),
            typeof(THandler));

        return services;
    }

    /// <summary>Registers one typed command handler and its exact route.</summary>
    public static IServiceCollection AddSliceForgeCommandHandler<TCommand, TResponse, THandler>(
        this IServiceCollection services)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        RouteRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);
        HandlerRouteRegistration.Register(
            services,
            registrationStore,
            new TypedCommandRoute<TCommand, TResponse>(),
            typeof(ICommandHandler<TCommand, TResponse>),
            typeof(THandler));

        return services;
    }

    /// <summary>Registers one query handler and its exact route.</summary>
    public static IServiceCollection AddSliceForgeQueryHandler<TQuery, TResponse, THandler>(
        this IServiceCollection services)
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        RouteRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);
        HandlerRouteRegistration.Register(
            services,
            registrationStore,
            new QueryRoute<TQuery, TResponse>(),
            typeof(IQueryHandler<TQuery, TResponse>),
            typeof(THandler));

        return services;
    }

    private static RouteRegistrationStore GetOrCreateRegistrationStore(IServiceCollection services)
    {
        RouteRegistrationStore? existingStore = FindRegistrationStore(services);
        if (existingStore is not null)
        {
            return existingStore;
        }

        RouteRegistrationStore newRegistrationStore = new();
        services.AddSingleton(newRegistrationStore);
        return newRegistrationStore;
    }

    private static RouteRegistrationStore? FindRegistrationStore(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(RouteRegistrationStore) &&
                descriptor.ImplementationInstance is RouteRegistrationStore registrationStore)
            {
                return registrationStore;
            }
        }

        return null;
    }
}
