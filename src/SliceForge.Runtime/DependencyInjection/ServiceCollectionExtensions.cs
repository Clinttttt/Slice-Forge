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

    /// <summary>Registers one non-generic command handler and its exact route.</summary>
    public static IServiceCollection AddSliceForgeCommandHandler<TCommand, THandler>(
        this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        ArgumentNullException.ThrowIfNull(services);

        RouteRegistrationStore registrationStore = GetOrCreateRegistrationStore(services);
        registrationStore.Add(new CommandRoute<TCommand>());
        services.AddTransient<ICommandHandler<TCommand>, THandler>();

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
        registrationStore.Add(new TypedCommandRoute<TCommand, TResponse>());
        services.AddTransient<ICommandHandler<TCommand, TResponse>, THandler>();

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
        registrationStore.Add(new QueryRoute<TQuery, TResponse>());
        services.AddTransient<IQueryHandler<TQuery, TResponse>, THandler>();

        return services;
    }

    private static RouteRegistrationStore GetOrCreateRegistrationStore(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(RouteRegistrationStore) &&
                descriptor.ImplementationInstance is RouteRegistrationStore registrationStore)
            {
                return registrationStore;
            }
        }

        RouteRegistrationStore newRegistrationStore = new();
        services.AddSingleton(newRegistrationStore);
        return newRegistrationStore;
    }
}
