using System.IO;
using System.Windows;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Data;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace ChaosAssetManager.Controls.PreviewControls;

public sealed partial class EntryPreviewControl : IDisposable
{
    private readonly DataArchive Archive = null!;
    private readonly string ArchiveName = null!;
    private readonly string ArchiveRoot = null!;
    private readonly DataArchiveEntry Entry = null!;
    private readonly Lock Sync;
    private Animation? Animation;

    // ReSharper disable once NotAccessedField.Local
    private Task? AnimationTask;
    private PeriodicTimer? AnimationTimer;
    private int CurrentFrameIndex;
    private int Disposed;
    private SKGLElementPlus? Element;

    public EntryPreviewControl(
        DataArchive archive,
        DataArchiveEntry entry,
        string archiveName,
        string archiveRoot)
    {
        Archive = archive;
        Entry = entry;
        ArchiveName = archiveName;
        ArchiveRoot = archiveRoot;
        Sync = new Lock();

        InitializeComponent();
        Initialize();
    }

    public EntryPreviewControl(Animation animation)
    {
        Animation = animation;
        Sync = new Lock();

        InitializeComponent();
        GenerateElement();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.EnterScope();

        if (Interlocked.CompareExchange(ref Disposed, 1, 0) == 1)
            return;

        Animation?.Dispose();
        AnimationTimer?.Dispose();
        Element?.Dispose();

        Animation = null;
        AnimationTimer = null;
        Element = null;
    }

    private void Initialize()
    {
        var type = Path.GetExtension(Entry.EntryName)
                       .ToLower();

        switch (type)
        {
            case ".tbl":
            case ".txt":
            case ".log":
            {
                Content = RenderUtil.RenderText(Entry);

                break;
            }
            case ".pal":
            {
                var animation = RenderUtil.RenderPalette(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".efa":
            {
                var animation = RenderUtil.RenderEfa(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }

            case ".spf":
            {
                var animation = RenderUtil.RenderSpf(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".bmp":
            {
                if (ArchiveName == "seo.dat")
                    return;

                var animation = RenderUtil.RenderBmp(Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".mpf":
            {
                var animation = RenderUtil.RenderMpf(Archive, Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".epf":
            {
                var animation = RenderUtil.RenderEpf(
                    Archive,
                    Entry,
                    ArchiveName,
                    ArchiveRoot);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }

            case ".hpf":
            {
                var animation = RenderUtil.RenderHpf(Archive, Entry);

                if (animation is null)
                    break;

                Animation = animation;

                break;
            }
            case ".mp3":
            {
                var audioStream = Entry.ToStreamSegment();
                var player = new AudioPlayer(audioStream);

                Content = player;

                break;
            }
        }

        if (Animation is not null)
            GenerateElement();
    }

    #region Animation
    private async Task AnimateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(AnimationTimer);
        ArgumentNullException.ThrowIfNull(Element);

        try
        {
            while (true)
            {
                await AnimationTimer.WaitForNextTickAsync();

                using var @lock = Sync.EnterScope();

                if (Disposed == 1)
                    return;

                CurrentFrameIndex = (CurrentFrameIndex + 1) % Animation.Frames.Count;
                Element.Redraw();
            }
        } catch
        {
            //ignored
        }
    }

    private void GenerateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            AnimationTimer?.Dispose();
            AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
            Element?.Dispose();
            Element = new SKGLElementPlus();
            Element.Paint += ElementPaintSurface;
            Element.Loaded += ElementOnLoaded;

            if (Animation.Frames.Count > 1)
                AnimationTask = AnimateElement();

            Content = Element;
        } catch
        {
            //ignored
        }
    }

    private void ElementOnLoaded(object sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Element);
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var elementWidth = Element.ActualWidth * dpiScale;
            var elementHeight = Element.ActualHeight * dpiScale;
            var centerPoint = new SKPoint((float)elementWidth / 2f, (float)elementHeight / 2f);
            var maxWidth = Animation.Frames.Max(frame => frame.Width);
            var maxHeight = Animation.Frames.Max(frame => frame.Height);

            //calculate the translation to center the image
            var translateX = (float)(elementWidth - maxWidth) / 2f;
            var translateY = (float)(elementHeight - maxHeight) / 2f;

            //center the image
            Element.Matrix = SKMatrix.CreateTranslation(translateX, translateY);

            //invert the matrix
            if (!Element.Matrix.TryInvert(out var inverseMatrix))
                return;

            //get the center point of the image
            var transformedPoint = inverseMatrix.MapPoint(centerPoint);

            //scale up the image so that it fits better in the element, but not so big that it's bigger than the element
            //also, don't scale tiny images too much
            var scale = Math.Clamp(Math.Min((float)elementWidth / maxWidth / 1.2f, (float)elementHeight / maxHeight / 1.2f), 1.0f, 2.0f);

            //scale the image up around the center of the image so that it stays centered
            Element.Matrix = Element.Matrix.PreConcat(
                SKMatrix.CreateScale(
                    scale,
                    scale,
                    transformedPoint.X,
                    transformedPoint.Y));

            Element.Redraw();
        } catch
        {
            //ignored
        }
    }

    private void ElementPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            using var @lock = Sync.EnterScope();

            if (Disposed == 1)
                return;

            var canvas = e.Surface.Canvas;
            var frame = Animation.Frames[CurrentFrameIndex];

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            //sometimes frames can be null
            if (frame is null)
                return;

            var targetX = 0;
            var targetY = 0;

            canvas.DrawImage(frame, targetX, targetY);

            canvas.Restore();
        } catch
        {
            //ignored
        }
    }
    #endregion
}