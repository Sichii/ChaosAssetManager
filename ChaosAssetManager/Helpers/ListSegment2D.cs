namespace ChaosAssetManager.Helpers;

public readonly ref struct ListSegment2D<T>
{
    private readonly IList<T> Origin;
    private readonly int OffsetX;
    private readonly int OffsetY;
    private readonly int Stride;
    public int Width { get; }
    public int Height { get; }

    public RowEnumerator Rows => new(this);

    public T this[int x, int y]
    {
        get => Origin[(OffsetY + y) * Stride + OffsetX + x];
        set => Origin[(OffsetY + y) * Stride + OffsetX + x] = value;
    }

    public ListSegment2D(IList<T> origin, int width)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");

        if ((origin.Count % width) != 0)
            throw new ArgumentException("The span's length must be evenly divisible by the width.", nameof(width));

        Origin = origin;
        Stride = width;
        OffsetX = 0;
        OffsetY = 0;
        Width = width;
        Height = origin.Count / width;
    }

    public ListSegment2D(
        ListSegment2D<T> source,
        int offsetX,
        int offsetY,
        int width,
        int height)
    {
        Origin = source.Origin;
        Stride = source.Stride;
        OffsetX = source.OffsetX + offsetX;
        OffsetY = source.OffsetY + offsetY;
        Width = width;
        Height = height;
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

        public readonly ListSegment<T> Current
            => new(_parent.Origin, (_parent.OffsetY + _currentRow) * _parent.Stride + _parent.OffsetX, _parent.Width);

        public readonly RowEnumerator GetEnumerator() => this;
    }
}