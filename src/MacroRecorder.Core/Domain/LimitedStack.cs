namespace MacroRecorder.Core.Domain;

public sealed class LimitedStack<T>
{
    private readonly int _maxSize;
    private readonly LinkedList<T> _items = new();

    public LimitedStack(int maxSize)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be > 0.");
        }

        _maxSize = maxSize;
    }

    public int Count => _items.Count;

    public void Push(T item)
    {
        _items.AddLast(item);
        if (_items.Count > _maxSize)
        {
            _items.RemoveFirst();
        }
    }

    public bool TryPop(out T? item)
    {
        if (_items.Count == 0)
        {
            item = default;
            return false;
        }

        var node = _items.Last!;
        item = node.Value;
        _items.RemoveLast();
        return true;
    }

    public void Clear() => _items.Clear();
}
