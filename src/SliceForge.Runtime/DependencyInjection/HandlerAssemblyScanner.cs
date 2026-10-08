using System.Reflection;
using SliceForge.Messaging;
using SliceForge.Runtime.Routing;

namespace SliceForge.Runtime.DependencyInjection;

internal static class HandlerAssemblyScanner
{
    public static IReadOnlyList<DiscoveredHandlerRegistration> Discover(Assembly assembly)
    {
        Type[] types = GetTypes(assembly);
        List<DiscoveredHandlerRegistration> registrations = [];

        foreach (Type handlerType in types
                     .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
                     .OrderBy(GetStableTypeName, StringComparer.Ordinal))
        {
            Type[] contracts;
            try
            {
                contracts = handlerType.GetInterfaces();
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                throw CreateInspectionException(assembly, handlerType, exception);
            }

            foreach (Type contract in contracts
                         .Where(IsSupportedHandlerContract)
                         .OrderBy(GetStableTypeName, StringComparer.Ordinal))
            {
                Type[] genericArguments = contract.GetGenericArguments();
                Type messageType = genericArguments[0];
                Type routeType = GetRouteType(contract.GetGenericTypeDefinition(), genericArguments);
                IMessageRoute route = CreateRoute(routeType, assembly, handlerType, contract);

                registrations.Add(new DiscoveredHandlerRegistration(
                    messageType,
                    contract,
                    handlerType,
                    route));
            }
        }

        return registrations
            .OrderBy(registration => GetStableTypeName(registration.MessageType), StringComparer.Ordinal)
            .ThenBy(registration => GetStableTypeName(registration.HandlerType), StringComparer.Ordinal)
            .ThenBy(registration => GetStableTypeName(registration.HandlerContract), StringComparer.Ordinal)
            .ToArray();
    }

    public static void ThrowIfConflictingRoutes(
        Assembly assembly,
        IReadOnlyCollection<DiscoveredHandlerRegistration> registrations,
        IReadOnlyCollection<IMessageRoute> existingRoutes)
    {
        Dictionary<Type, IMessageRoute> existingByMessage = existingRoutes.ToDictionary(route => route.MessageType);
        IGrouping<Type, DiscoveredHandlerRegistration>[] duplicateGroups = registrations
            .GroupBy(registration => registration.MessageType)
            .Where(group => group.Count() > 1)
            .OrderBy(group => GetStableTypeName(group.Key), StringComparer.Ordinal)
            .ToArray();

        List<string> conflicts = [];
        foreach (IGrouping<Type, DiscoveredHandlerRegistration> group in duplicateGroups)
        {
            string handlers = string.Join(
                ", ",
                group.Select(registration =>
                    $"'{GetStableTypeName(registration.HandlerType)}' ({GetStableTypeName(registration.HandlerContract)})"));
            conflicts.Add($"Message '{GetStableTypeName(group.Key)}' has multiple discovered handlers: {handlers}.");
        }

        foreach (DiscoveredHandlerRegistration registration in registrations)
        {
            if (existingByMessage.ContainsKey(registration.MessageType))
            {
                conflicts.Add(
                    $"Message '{GetStableTypeName(registration.MessageType)}' already has a registered route; " +
                    $"assembly '{GetAssemblyName(assembly)}' discovered handler " +
                    $"'{GetStableTypeName(registration.HandlerType)}'.");
            }
        }

        if (conflicts.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot register handlers from assembly '{GetAssemblyName(assembly)}' because route conflicts were found: " +
                string.Join(" ", conflicts));
        }
    }

    private static Type[] GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            string loaderDetails = string.Join(
                " | ",
                exception.LoaderExceptions
                    .OfType<Exception>()
                    .Select(loaderException => $"{loaderException.GetType().Name}: {loaderException.Message}"));
            string details = string.IsNullOrWhiteSpace(loaderDetails)
                ? "One or more types could not be loaded."
                : loaderDetails;

            throw new InvalidOperationException(
                $"Unable to inspect all types in assembly '{GetAssemblyName(assembly)}'. {details}",
                exception);
        }
        catch (TypeLoadException exception)
        {
            throw new InvalidOperationException(
                $"Unable to inspect types in assembly '{GetAssemblyName(assembly)}'. {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            throw new InvalidOperationException(
                $"Unable to inspect types in assembly '{GetAssemblyName(assembly)}'. {exception.Message}",
                exception);
        }
    }

    private static InvalidOperationException CreateInspectionException(
        Assembly assembly,
        Type handlerType,
        Exception exception) =>
        new(
            $"Unable to inspect handler interfaces for type '{GetStableTypeName(handlerType)}' " +
            $"in assembly '{GetAssemblyName(assembly)}'. {GetFailureDetails(exception)}",
            exception);

    private static bool IsReflectionFailure(Exception exception) =>
        exception is ReflectionTypeLoadException or TypeLoadException or FileNotFoundException or
            FileLoadException or BadImageFormatException;

    private static string GetFailureDetails(Exception exception)
    {
        if (exception is not ReflectionTypeLoadException typeLoadException)
        {
            return exception.Message;
        }

        string loaderDetails = string.Join(
            " | ",
            typeLoadException.LoaderExceptions
                .OfType<Exception>()
                .Select(loaderException => $"{loaderException.GetType().Name}: {loaderException.Message}"));

        return string.IsNullOrWhiteSpace(loaderDetails)
            ? typeLoadException.Message
            : loaderDetails;
    }

    private static bool IsSupportedHandlerContract(Type contract)
    {
        if (!contract.IsGenericType || contract.ContainsGenericParameters)
        {
            return false;
        }

        Type genericDefinition = contract.GetGenericTypeDefinition();
        return genericDefinition == typeof(ICommandHandler<>) ||
            genericDefinition == typeof(ICommandHandler<,>) ||
            genericDefinition == typeof(IQueryHandler<,>);
    }

    private static Type GetRouteType(Type contractDefinition, Type[] genericArguments)
    {
        Type routeDefinition = contractDefinition == typeof(ICommandHandler<>)
            ? typeof(CommandRoute<>)
            : contractDefinition == typeof(ICommandHandler<,>)
                ? typeof(TypedCommandRoute<,>)
                : typeof(QueryRoute<,>);

        return routeDefinition.MakeGenericType(genericArguments);
    }

    private static IMessageRoute CreateRoute(
        Type routeType,
        Assembly assembly,
        Type handlerType,
        Type contract)
    {
        try
        {
            return (IMessageRoute?)Activator.CreateInstance(routeType, nonPublic: true)
                ?? throw new InvalidOperationException(
                    $"Could not create route metadata for handler '{GetStableTypeName(handlerType)}' " +
                    $"and contract '{GetStableTypeName(contract)}'.");
        }
        catch (Exception exception) when (exception is ArgumentException or MemberAccessException or TargetInvocationException)
        {
            throw new InvalidOperationException(
                $"Could not create route metadata for handler '{GetStableTypeName(handlerType)}' " +
                $"in assembly '{GetAssemblyName(assembly)}'.",
                exception is TargetInvocationException { InnerException: not null } invocationException
                    ? invocationException.InnerException
                    : exception);
        }
    }

    private static string GetAssemblyName(Assembly assembly) =>
        assembly.FullName ?? assembly.GetName().Name ?? "<unknown assembly>";

    private static string GetStableTypeName(Type type) =>
        type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
}

internal sealed record DiscoveredHandlerRegistration(
    Type MessageType,
    Type HandlerContract,
    Type HandlerType,
    IMessageRoute Route);
