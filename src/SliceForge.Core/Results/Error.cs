using System.Collections.ObjectModel;

namespace SliceForge.Results;

/// <summary>
/// Describes an expected application error without transport-specific concerns.
/// </summary>
public sealed class Error
{
    /// <summary>
    /// Initializes an immutable error description.
    /// </summary>
    /// <param name="code">The stable application-facing error code.</param>
    /// <param name="description">The user-safe error description.</param>
    /// <param name="type">The broad category of the error.</param>
    /// <param name="validationErrors">Optional field-level validation messages.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a required text value is blank or validation details are invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when a required value is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type" /> is not defined.</exception>
    public Error(
        string code,
        string description,
        ErrorType type,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? validationErrors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "The error type is not defined.");
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> copiedValidationErrors =
            CopyValidationErrors(validationErrors);

        if (copiedValidationErrors.Count > 0 && type != ErrorType.Validation)
        {
            throw new ArgumentException(
                "Validation details require ErrorType.Validation.",
                nameof(validationErrors));
        }

        Code = code;
        Description = description;
        Type = type;
        ValidationErrors = copiedValidationErrors;
    }

    /// <summary>
    /// Gets the stable application-facing error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the user-safe error description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the broad category of the error.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Gets the immutable field-level validation messages, if any.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CopyValidationErrors(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? validationErrors)
    {
        Dictionary<string, IReadOnlyList<string>> copy = new(StringComparer.Ordinal);

        if (validationErrors is null)
        {
            return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
        }

        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in validationErrors)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException(
                    "Validation error keys cannot be null.",
                    nameof(validationErrors));
            }

            if (entry.Value is null)
            {
                throw new ArgumentException(
                    "Validation error messages cannot be null.",
                    nameof(validationErrors));
            }

            if (entry.Value.Count == 0)
            {
                throw new ArgumentException(
                    "Each validation error must contain at least one message.",
                    nameof(validationErrors));
            }

            string[] messageCopy = new string[entry.Value.Count];

            for (int index = 0; index < entry.Value.Count; index++)
            {
                string message = entry.Value[index];
                ArgumentException.ThrowIfNullOrWhiteSpace(message, nameof(validationErrors));
                messageCopy[index] = message;
            }

            copy.Add(entry.Key, Array.AsReadOnly(messageCopy));
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }
}
