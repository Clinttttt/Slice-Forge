using System.Threading;
using System.Threading.Tasks;
using SliceForge.Results;

namespace SliceForge.Messaging;

/// <summary>
/// Handles a query with a typed value payload.
/// </summary>
/// <typeparam name="TQuery">The query type handled by this handler.</typeparam>
/// <typeparam name="TResponse">The successful value type produced by the query.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles the query asynchronously.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">The token used to observe cancellation.</param>
    /// <returns>The query result containing the response value when successful.</returns>
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
