namespace SliceForge.Messaging;

/// <summary>
/// Marks a command whose logical result contains a typed value payload.
/// </summary>
/// <typeparam name="TResponse">The successful value type produced by the command.</typeparam>
public interface ICommand<TResponse> : IBaseCommand
{
}
