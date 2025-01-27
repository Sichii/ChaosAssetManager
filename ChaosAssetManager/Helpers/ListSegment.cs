namespace ChaosAssetManager.Helpers;

public readonly ref struct ListSegment<T>
{
    private readonly IList<T> _list;
    private readonly int _offset;

    public int Count { get; }

    public ListSegment(IList<T> list, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(list);

        if ((offset < 0) || (offset > list.Count))
            throw new ArgumentOutOfRangeException(nameof(offset));

        if ((count < 0) || ((offset + count) > list.Count))
            throw new ArgumentOutOfRangeException(nameof(count));

        _list = list;
        _offset = offset;
        Count = count;
    }

    public T this[int index]
    {
        get
        {
            if ((index < 0) || (index >= Count))
                throw new ArgumentOutOfRangeException(nameof(index));

            return _list[_offset + index];
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        private readonly ListSegment<T> _segment;
        private int _index;

        public Enumerator(ListSegment<T> segment)
        {
            _segment = segment;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;

            return _index < _segment.Count;
        }

        public readonly T Current => _segment[_index];

        public readonly Enumerator GetEnumerator() => this;
    }
}