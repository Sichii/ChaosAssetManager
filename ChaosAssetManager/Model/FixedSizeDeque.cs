namespace ChaosAssetManager.Model;

public class FixedSizeDeque<T>
{
    private readonly LinkedList<T> List = [];
    private readonly int MaxSize;

    /// <summary>
    ///     Returns the current number of items in the deque.
    /// </summary>
    public int Count => List.Count;

    /// <summary>
    ///     Returns the items in the deque as a read-only collection.
    /// </summary>
    public IReadOnlyCollection<T> Items => List;

    public FixedSizeDeque(int maxSize)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than zero.");

        MaxSize = maxSize;
    }

    /// <summary>
    ///     Adds an item to the back (newest end). Removes the oldest item if the deque exceeds its maximum size.
    /// </summary>
    public void AddNewest(T item)
    {
        if (List.Count == MaxSize)
            List.RemoveFirst(); // Remove the oldest item

        List.AddLast(item); // Add to the newest end
    }

    /// <summary>
    ///     Adds an item to the front (oldest end). Removes the newest item if the deque exceeds its maximum size.
    /// </summary>
    public void AddOldest(T item)
    {
        if (List.Count == MaxSize)
            List.RemoveLast(); // Remove the newest item

        List.AddFirst(item); // Add to the oldest end
    }

    /// <summary>
    ///     Clears the deque.
    /// </summary>
    public void Clear() => List.Clear();

    /// <summary>
    ///     Returns the newest item without removing it.
    /// </summary>
    public T PeekNewest()
    {
        if (List.Count == 0)
            throw new InvalidOperationException("The deque is empty.");

        return List.Last!.Value;
    }

    /// <summary>
    ///     Returns the oldest item without removing it.
    /// </summary>
    public T PeekOldest()
    {
        if (List.Count == 0)
            throw new InvalidOperationException("The deque is empty.");

        return List.First!.Value;
    }

    /// <summary>
    ///     Removes and returns the newest item.
    /// </summary>
    public T PopNewest()
    {
        if (List.Count == 0)
            throw new InvalidOperationException("The deque is empty.");

        var newest = List.Last!.Value;
        List.RemoveLast();

        return newest;
    }

    /// <summary>
    ///     Removes and returns the oldest item.
    /// </summary>
    public T PopOldest()
    {
        if (List.Count == 0)
            throw new InvalidOperationException("The deque is empty.");

        var oldest = List.First!.Value;
        List.RemoveFirst();

        return oldest;
    }
}