using Microsoft.Extensions.DependencyInjection;
using SliceForge.Runtime.Routing;

namespace SliceForge.Runtime.DependencyInjection;

internal static class HandlerRouteRegistration
{
    public static void Register(
        IServiceCollection services,
        RouteRegistrationStore registrationStore,
        IMessageRoute route,
        Type handlerContract,
        Type handlerType)
    {
        registrationStore.Add(route);
        services.AddTransient(handlerContract, handlerType);
    }
}
