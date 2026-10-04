using SliceForge.Sample.Api.Features.Todos;

namespace SliceForge.Sample.Api.Infrastructure;

internal sealed class TodoStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, TodoEntry> _todos = [];

    public Guid Create(string title)
    {
        Guid id = Guid.NewGuid();

        lock (_syncRoot)
        {
            _todos.Add(id, new TodoEntry(title));
        }

        return id;
    }

    public TodoResponse? Find(Guid id)
    {
        lock (_syncRoot)
        {
            return _todos.TryGetValue(id, out TodoEntry? entry)
                ? new TodoResponse(id, entry.Title, entry.IsCompleted)
                : null;
        }
    }

    public TodoCompletionOutcome Complete(Guid id)
    {
        lock (_syncRoot)
        {
            if (!_todos.TryGetValue(id, out TodoEntry? entry))
            {
                return TodoCompletionOutcome.NotFound;
            }

            if (entry.IsCompleted)
            {
                return TodoCompletionOutcome.AlreadyCompleted;
            }

            entry.IsCompleted = true;
            return TodoCompletionOutcome.Completed;
        }
    }

    private sealed class TodoEntry(string title)
    {
        public string Title { get; } = title;
        public bool IsCompleted { get; set; }
    }
}

internal enum TodoCompletionOutcome
{
    Completed,
    NotFound,
    AlreadyCompleted
}
