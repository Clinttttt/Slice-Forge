namespace SliceForge.Results;

/// <summary>
/// Classifies an expected application outcome without coupling it to a transport protocol.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Represents a general application failure.
    /// </summary>
    Failure,

    /// <summary>
    /// Represents invalid input or state validation.
    /// </summary>
    Validation,

    /// <summary>
    /// Represents a requested resource that could not be found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Represents an operation that conflicts with the current application state.
    /// </summary>
    Conflict,

    /// <summary>
    /// Represents an operation that requires authorization.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// Represents an operation that the caller is not permitted to perform.
    /// </summary>
    Forbidden
}
