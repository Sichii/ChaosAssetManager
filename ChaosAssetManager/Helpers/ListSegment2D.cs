namespace ChaosAssetManager.Helpers;

public readonly ref struct ListSegment2D<T>
{
    private readonly IList<T> Origin;
    public int Width { get; }
    public int Height { get; }

    public RowEnumerator Rows => new(this);

    public T this[int x, int y]
    {
        get => Origin[y * Width + x];
        set => Origin[y * Width + x] = value;
    }

    public ListSegment2D(IList<T> origin, int width)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");

        if ((origin.Count % width) != 0)
            throw new ArgumentException("The span's length must be evenly divisible by the width.", nameof(width));

        Origin = origin;
        Width = width;
        Height = origin.Count / width;
    }

    public ref struct RowEnumerator
    {
        private readonly ListSegment2D<T> _parent;
        private int _currentRow;

        public RowEnumerator(ListSegment2D<T> parent)
        {
            _parent = parent;
            _currentRow = -1;
        }

        public bool MoveNext() => ++_currentRow < _parent.Height;

        public readonly ListSegment<T> Current => new(_parent.Origin, _currentRow * _parent.Width, _parent.Width);

        public readonly RowEnumerator GetEnumerator() => this;
    }
}