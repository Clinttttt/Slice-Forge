using Microsoft.AspNetCore.Http;
using SliceForge.Results;

namespace SliceForge.AspNetCore.Results;

/// <summary>
/// Maps SliceForge results to ASP.NET Core HTTP results.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// Maps a non-generic result to an HTTP result, leaving successful response semantics to the caller.
    /// </summary>
    /// <param name="result">The application result to map.</param>
    /// <param name="onSuccess">Creates the HTTP result for a successful application result.</param>
    /// <returns>The caller-provided HTTP result on success, or a mapped expected-failure result.</returns>
    /// <exception cref="ArgumentNullException">The result or success callback is null.</exception>
    /// <exception cref="InvalidOperationException">The result is invalid or the success callback returns null.</exception>
    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            return onSuccess()
                ?? throw new InvalidOperationException("The success callback returned a null HTTP result.");
        }

        Error error = result.Error
            ?? throw new InvalidOperationException("A failed result must contain an error.");

        return MapFailure(error);
    }

    /// <summary>
    /// Maps a typed result to an HTTP result, leaving successful response semantics to the caller.
    /// </summary>
    /// <typeparam name="TResponse">The successful result value type.</typeparam>
    /// <param name="result">The application result to map.</param>
    /// <param name="onSuccess">Creates the HTTP result for a successful application result.</param>
    /// <returns>The caller-provided HTTP result on success, or a mapped expected-failure result.</returns>
    /// <exception cref="ArgumentNullException">The result or success callback is null.</exception>
    /// <exception cref="InvalidOperationException">The result is invalid or the success callback returns null.</exception>
    public static IResult ToHttpResult<TResponse>(
        this Result<TResponse> result,
        Func<TResponse, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            return onSuccess(result.Value)
                ?? throw new InvalidOperationException("The success callback returned a null HTTP result.");
        }

        Error error = result.Error
            ?? throw new InvalidOperationException("A failed result must contain an error.");

        return MapFailure(error);
    }

    private static IResult MapFailure(Error error)
    {
        return error.Type switch
        {
            ErrorType.Failure => CreateProblem(error, StatusCodes.Status400BadRequest),
            ErrorType.Validation => CreateValidationProblem(error),
            ErrorType.NotFound => CreateProblem(error, StatusCodes.Status404NotFound),
            ErrorType.Conflict => CreateProblem(error, StatusCodes.Status409Conflict),
            ErrorType.Unauthorized => Microsoft.AspNetCore.Http.Results.Challenge(),
            ErrorType.Forbidden => Microsoft.AspNetCore.Http.Results.Forbid(),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "The error type is not supported.")
        };
    }

    private static IResult CreateProblem(Error error, int statusCode)
    {
        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: statusCode,
            detail: error.Description,
            extensions: CreateExtensions(error));
    }

    private static IResult CreateValidationProblem(Error error)
    {
        Dictionary<string, string[]> validationErrors = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in error.ValidationErrors)
        {
            HashSet<string> seenMessages = new(StringComparer.Ordinal);
            List<string> uniqueMessages = new(entry.Value.Count);

            foreach (string message in entry.Value)
            {
                if (seenMessages.Add(message))
                {
                    uniqueMessages.Add(message);
                }
            }

            validationErrors.Add(entry.Key, uniqueMessages.ToArray());
        }

        return Microsoft.AspNetCore.Http.Results.ValidationProblem(
            validationErrors,
            detail: error.Description,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: CreateExtensions(error));
    }

    private static Dictionary<string, object?> CreateExtensions(Error error)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code
        };
    }
}
