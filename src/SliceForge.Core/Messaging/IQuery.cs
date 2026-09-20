namespace SliceForge.Messaging;

/// <summary>
/// Marks a query that produces a typed value payload.
/// </summary>
/// <typeparam name="TResponse">The successful value type produced by the query.</typeparam>
public interface IQuery<TResponse>
{
}
