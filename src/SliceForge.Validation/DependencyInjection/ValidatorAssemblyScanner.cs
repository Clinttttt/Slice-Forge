using System.Reflection;
using FluentValidation;
using SliceForge.Validation.Routing;

namespace SliceForge.Validation.DependencyInjection;

internal static class ValidatorAssemblyScanner
{
    public static IReadOnlyList<DiscoveredValidatorRegistration> Discover(Assembly assembly)
    {
        Type[] types = GetTypes(assembly);
        List<DiscoveredValidatorRegistration> registrations = [];

        foreach (Type validatorType in types
                     .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
                     .OrderBy(GetStableTypeName, StringComparer.Ordinal))
        {
            Type[] contracts;
            try
            {
                contracts = validatorType.GetInterfaces();
            }
            catch (Exception exception)
            {
                throw CreateInspectionException(assembly, validatorType, exception);
            }

            foreach (Type contract in contracts
                         .Where(IsSupportedValidatorContract)
                         .OrderBy(GetStableTypeName, StringComparer.Ordinal))
            {
                Type messageType = contract.GetGenericArguments()[0];
                IValidationRoute route = CreateRoute(assembly, validatorType, messageType);
                registrations.Add(new DiscoveredValidatorRegistration(messageType, validatorType, route));
            }
        }

        DiscoveredValidatorRegistration[] orderedRegistrations = registrations
            .OrderBy(registration => GetStableTypeName(registration.MessageType), StringComparer.Ordinal)
            .ThenBy(registration => GetStableTypeName(registration.ValidatorType), StringComparer.Ordinal)
            .ToArray();

        IGrouping<(Type MessageType, Type ValidatorType), DiscoveredValidatorRegistration>[] duplicates =
            orderedRegistrations
                .GroupBy(registration => (registration.MessageType, registration.ValidatorType))
                .Where(group => group.Count() > 1)
                .ToArray();
        if (duplicates.Length > 0)
        {
            IGrouping<(Type MessageType, Type ValidatorType), DiscoveredValidatorRegistration> duplicate =
                duplicates[0];
            throw new InvalidOperationException(
                $"Assembly '{GetAssemblyName(assembly)}' contains duplicate validator contract " +
                $"'{GetStableTypeName(duplicate.Key.ValidatorType)}' for message " +
                $"'{GetStableTypeName(duplicate.Key.MessageType)}'.");
        }

        return orderedRegistrations;
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
                    .Select(loaderException =>
                        $"{loaderException.GetType().Name}: {loaderException.Message}"));
            string details = string.IsNullOrWhiteSpace(loaderDetails)
                ? "One or more types could not be loaded."
                : loaderDetails;

            throw new InvalidOperationException(
                $"Unable to inspect all types in validator assembly '{GetAssemblyName(assembly)}'. {details}",
                exception);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Unable to inspect validator assembly '{GetAssemblyName(assembly)}'. {exception.Message}",
                exception);
        }
    }

    private static IValidationRoute CreateRoute(
        Assembly assembly,
        Type validatorType,
        Type messageType)
    {
        try
        {
            Type routeType = typeof(ValidationRoute<>).MakeGenericType(messageType);
            return (IValidationRoute?)Activator.CreateInstance(routeType, validatorType)
                ?? throw new InvalidOperationException(
                    $"Could not create validation route metadata for validator " +
                    $"'{GetStableTypeName(validatorType)}' and message '{GetStableTypeName(messageType)}'.");
        }
        catch (Exception exception)
        {
            Exception innerException = exception is TargetInvocationException
            { InnerException: not null } invocationException
                ? invocationException.InnerException!
                : exception;
            throw new InvalidOperationException(
                $"Could not create validation route for validator '{GetStableTypeName(validatorType)}' " +
                $"and message '{GetStableTypeName(messageType)}' in assembly '{GetAssemblyName(assembly)}'. " +
                GetFailureDetails(innerException),
                innerException);
        }
    }

    private static InvalidOperationException CreateInspectionException(
        Assembly assembly,
        Type validatorType,
        Exception exception) =>
        new(
            $"Unable to inspect validator interfaces for type '{GetStableTypeName(validatorType)}' " +
            $"in assembly '{GetAssemblyName(assembly)}'. {GetFailureDetails(exception)}",
            exception);

    private static bool IsSupportedValidatorContract(Type contract) =>
        contract.IsGenericType &&
        !contract.ContainsGenericParameters &&
        contract.GetGenericTypeDefinition() == typeof(IValidator<>);

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
                .Select(loaderException =>
                    $"{loaderException.GetType().Name}: {loaderException.Message}"));

        return string.IsNullOrWhiteSpace(loaderDetails)
            ? typeLoadException.Message
            : loaderDetails;
    }

    private static string GetAssemblyName(Assembly assembly) =>
        assembly.FullName ?? assembly.GetName().Name ?? "<unknown assembly>";

    private static string GetStableTypeName(Type type) =>
        type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
}

internal sealed record DiscoveredValidatorRegistration(
    Type MessageType,
    Type ValidatorType,
    IValidationRoute Route);
