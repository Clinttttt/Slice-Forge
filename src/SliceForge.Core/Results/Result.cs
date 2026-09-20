namespace SliceForge.Results;

/// <summary>
/// Represents an expected application outcome that either succeeded or contains an error.
/// </summary>
public class Result
{
    /// <summary>
    /// Initializes a result for use by SliceForge result types.
    /// </summary>
    /// <param name="error">The failure error, or <see langword="null" /> for success.</param>
    private protected Result(Error? error)
    {
        IsSuccess = error is null;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the result represents success.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the failure error, or <see langword="null" /> when the result succeeded.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Creates a successful result without a value payload.
    /// </summary>
    public static Result Success() => new(null);

    /// <summary>
    /// Creates a failed result with the supplied error.
    /// </summary>
    /// <param name="error">The expected application error.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is null.</exception>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(error);
    }
}
