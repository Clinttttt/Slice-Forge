namespace SliceForge.Results;

/// <summary>
/// Represents an expected application outcome that may contain a value on success.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(error)
    {
        _value = default;
    }

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result has failed.</exception>
    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("A failed result does not contain a value.");
            }

            return _value!;
        }
    }

    /// <summary>
    /// Creates a successful result containing the supplied value.
    /// </summary>
    /// <param name="value">The success value, including a nullable value when permitted by <typeparamref name="T" />.</param>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the supplied error.
    /// </summary>
    /// <param name="error">The expected application error.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is null.</exception>
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(error);
    }
}
