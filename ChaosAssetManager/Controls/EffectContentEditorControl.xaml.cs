using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Graphics = DALib.Drawing.Graphics;
using Palette = DALib.Drawing.Palette;

namespace ChaosAssetManager.Controls;

public sealed partial class EffectContentEditorControl : IDisposable, INotifyPropertyChanged
{
    private const int BODY_CENTER_X = 28;
    private const int BODY_CENTER_Y = 70;

    private readonly Lock Sync = new();

    private Animation? Animation;
    private CancellationTokenSource? AnimationCts;
    private PeriodicTimer? AnimationTimer;
    private Animation? BodyAnimation;
    private int CurrentFrameIndex;
    private ViewDirection CurrentViewDirection = ViewDirection.Right;
    private bool Disposed;
    private bool IsPlaying;

    public int FrameIntervalMs
    {
        get => IsEfa ? EfaFile?.FrameIntervalMs ?? 250 : 250;

        set
        {
            if (IsEfa && EfaFile is not null && (value > 0))
            {
                EfaFile.FrameIntervalMs = value;
                AnimationTimer?.Dispose();
                AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(value));
            }

            OnPropertyChanged();
        }
    }

    public int SelectedFrameIndex
    {
        get;

        set
        {
            if (SetField(ref field, value) && (value >= 0))
                PreviewElement?.Redraw();
        }
    }

    public List<SKPoint>? CenterPoints { get; }

    // EFA-specific data
    public EfaFile? EfaFile { get; }

    // EPF-specific data
    public EpfFile? EpfFile { get; }

    // Determines if this is an EFA or EPF effect
    public bool IsEfa { get; }
    public Palette? Palette { get; }

    public Visibility EfaSettingsVisibility => IsEfa ? Visibility.Visible : Visibility.Collapsed;

    // Constructor for EFA effects
    public EffectContentEditorControl(EfaFile efaFile)
    {
        IsEfa = true;
        EfaFile = efaFile;

        InitializeComponent();

        // Setup blending type dropdown
        BlendingTypeCmb.ItemsSource = new CollectionView(Enum.GetNames<EfaBlendingType>());
        BlendingTypeCmb.SelectedItem = efaFile.BlendingType.ToString();

        // Setup frame list
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, efaFile.Count));
        SelectedFrameIndex = 0;

        LoadBodyAnimation();
        RenderAnimation();
    }

    // Constructor for EPF effects
    public EffectContentEditorControl(EpfFile epfFile, Palette palette, List<SKPoint> centerPoints)
    {
        IsEfa = false;
        EpfFile = epfFile;
        Palette = palette;
        CenterPoints = centerPoints;

        InitializeComponent();

        // Setup frame list
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, epfFile.Count));
        SelectedFrameIndex = 0;

        LoadBodyAnimation();
        RenderAnimation();
    }

    public void Dispose()
    {
        using var @lock = Sync.EnterScope();
        Disposed = true;

        AnimationCts?.Cancel();
        AnimationTimer?.Dispose();
        Animation?.Dispose();
        BodyAnimation?.Dispose();
        PreviewElement?.Dispose();
    }

    private void LoadBodyAnimation()
    {
        if (string.IsNullOrEmpty(PathHelper.Instance.ArchivesPath))
            return;

        try
        {
            // Load male body by default (mb001)
            var archive = ArchiveCache.KhanMad;
            var palArchive = ArchiveCache.KhanPal;
            var palLookup = PaletteLookup.FromArchive("palb", palArchive);
            var bodyPalette = palLookup.GetPaletteForId(1, KhanPalOverrideType.Male);

            const string BODY_FILE_NAME = "mb00101.epf";

            if (!archive.Contains(BODY_FILE_NAME))
                return;

            var bodyEpf = EpfFile.FromEntry(archive[BODY_FILE_NAME]);
            var frames = bodyEpf.Select(frame => Graphics.RenderImage(frame, bodyPalette));
            BodyAnimation = new Animation(new SKImageCollection(frames), 250);
        } catch
        {
            // Ignore body loading errors - will just show effect without body
        }
    }

    private void RenderAnimation()
    {
        using var @lock = Sync.EnterScope();

        Animation?.Dispose();

        if (IsEfa)
        {
            var frames = EfaFile!.Select(frame => Graphics.RenderImage(frame, EfaFile!.BlendingType));
            Animation = new Animation(new SKImageCollection(frames), EfaFile!.FrameIntervalMs);
        } else
        {
            var frames = EpfFile!.Select(frame => Graphics.RenderImage(frame, Palette!));
            Animation = new Animation(new SKImageCollection(frames), 250);
        }

        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
        CurrentFrameIndex = 0;
    }

    private void ReRenderCurrentFrame()
    {
        using var @lock = Sync.EnterScope();

        if (Animation is null || (SelectedFrameIndex < 0))
            return;

        Animation.Frames[SelectedFrameIndex]
                 .Dispose();

        if (IsEfa)
            Animation.Frames[SelectedFrameIndex] = Graphics.RenderImage(EfaFile![SelectedFrameIndex], EfaFile.BlendingType);
        else
            Animation.Frames[SelectedFrameIndex] = Graphics.RenderImage(EpfFile![SelectedFrameIndex], Palette!);

        PreviewElement?.Redraw();
    }

    private enum ViewDirection
    {
        Up,
        Right,
        Down,
        Left
    }

    #region Movement Buttons
    private void MoveFrameUp_OnClick(object sender, RoutedEventArgs e) => MoveFramePosition(0, -1);
    private void MoveFrameDown_OnClick(object sender, RoutedEventArgs e) => MoveFramePosition(0, 1);
    private void MoveFrameLeft_OnClick(object sender, RoutedEventArgs e) => MoveFramePosition(-1, 0);
    private void MoveFrameRight_OnClick(object sender, RoutedEventArgs e) => MoveFramePosition(1, 0);

    private void MoveCenterUp_OnClick(object sender, RoutedEventArgs e) => MoveCenterPoint(0, -1);
    private void MoveCenterDown_OnClick(object sender, RoutedEventArgs e) => MoveCenterPoint(0, 1);
    private void MoveCenterLeft_OnClick(object sender, RoutedEventArgs e) => MoveCenterPoint(-1, 0);
    private void MoveCenterRight_OnClick(object sender, RoutedEventArgs e) => MoveCenterPoint(1, 0);

    private void MoveFramePosition(int dx, int dy)
    {
        if (SelectedFrameIndex < 0)
            return;

        var moveAll = MoveAllFramesChk.IsChecked == true;

        if (IsEfa)
        {
            var framesToMove = moveAll
                ? EfaFile!.ToList()
                : [EfaFile![SelectedFrameIndex]];

            foreach (var frame in framesToMove)
            {
                frame.Left = (short)(frame.Left + dx);
                frame.Top = (short)(frame.Top + dy);
            }
        }
        else
        {
            var framesToMove = moveAll
                ? EpfFile!.ToList()
                : [EpfFile![SelectedFrameIndex]];

            foreach (var frame in framesToMove)
            {
                frame.Left = (short)(frame.Left + dx);
                frame.Right = (short)(frame.Right + dx);
                frame.Top = (short)(frame.Top + dy);
                frame.Bottom = (short)(frame.Bottom + dy);
            }
        }

        ReRenderCurrentFrame();
    }

    private void MoveCenterPoint(int dx, int dy)
    {
        if (SelectedFrameIndex < 0)
            return;

        var moveAll = MoveAllCentersChk.IsChecked == true;

        if (IsEfa)
        {
            var framesToMove = moveAll
                ? EfaFile!.ToList()
                : [EfaFile![SelectedFrameIndex]];

            foreach (var frame in framesToMove)
            {
                frame.CenterX = (short)(frame.CenterX + dx);
                frame.CenterY = (short)(frame.CenterY + dy);
            }
        }
        else
        {
            if (moveAll)
            {
                for (var i = 0; i < CenterPoints!.Count; i++)
                {
                    var pt = CenterPoints[i];
                    CenterPoints[i] = new SKPoint(pt.X + dx, pt.Y + dy);
                }
            }
            else
            {
                var pt = CenterPoints![SelectedFrameIndex];
                CenterPoints[SelectedFrameIndex] = new SKPoint(pt.X + dx, pt.Y + dy);
            }
        }

        PreviewElement?.Redraw();
    }
    #endregion

    #region Event Handlers
    private void Direction_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DirectionCmb?.SelectedItem is ComboBoxItem item)
        {
            CurrentViewDirection = item.Content?.ToString() switch
            {
                "Up"    => ViewDirection.Up,
                "Right" => ViewDirection.Right,
                "Down"  => ViewDirection.Down,
                "Left"  => ViewDirection.Left,
                _       => ViewDirection.Right
            };
            PreviewElement?.Redraw();
        }
    }

    private void BlendingType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsEfa || EfaFile is null)
            return;

        if (BlendingTypeCmb.SelectedItem is string typeName && Enum.TryParse<EfaBlendingType>(typeName, out var blendType))
        {
            EfaFile.BlendingType = blendType;
            RenderAnimation();
            PreviewElement?.Redraw();
        }
    }

    private void Frame_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FramesListView.SelectedItem is int frameIndex)
        {
            SelectedFrameIndex = frameIndex;
            CurrentFrameIndex = frameIndex;
        }
    }

    private void ShowBoundsChk_OnChecked(object sender, RoutedEventArgs e) => PreviewElement?.Redraw();

    private void PlayPause_OnClick(object sender, RoutedEventArgs e)
    {
        IsPlaying = !IsPlaying;

        PlayPauseBtn.Content = new PackIcon
        {
            Kind = IsPlaying ? PackIconKind.Pause : PackIconKind.Play
        };

        if (IsPlaying)
            StartAnimation();
        else
            StopAnimation();
    }

    private void StartAnimation()
    {
        AnimationCts?.Cancel();
        AnimationCts = new CancellationTokenSource();
        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation?.FrameIntervalMs ?? 250));
        _ = AnimateAsync(AnimationCts.Token);
    }

    private void StopAnimation()
    {
        AnimationCts?.Cancel();
        AnimationTimer?.Dispose();
        AnimationTimer = null;
    }

    private async Task AnimateAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && AnimationTimer is not null && Animation is not null)
            {
                await AnimationTimer.WaitForNextTickAsync(ct);

                var frameCount = Animation.Frames.Count;

                if (frameCount > 0)
                {
                    CurrentFrameIndex = (CurrentFrameIndex + 1) % frameCount;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (CurrentFrameIndex < FramesListView.Items.Count)
                            FramesListView.SelectedIndex = CurrentFrameIndex;

                        PreviewElement?.Redraw();
                    });
                }
            }
        } catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }
    #endregion

    #region Rendering
    private void PreviewElement_OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var elementWidth = PreviewElement.ActualWidth;
            var elementHeight = PreviewElement.ActualHeight;

            var translateX = (float)(elementWidth / 2);
            var translateY = (float)(elementHeight / 2);

            PreviewElement.Matrix = SKMatrix.CreateTranslation(translateX, translateY);
            PreviewElement.Redraw();

            // Auto-start animation if multiple frames
            if ((Animation?.Frames.Count > 1) && !IsPlaying)
            {
                IsPlaying = true;

                PlayPauseBtn.Content = new PackIcon
                {
                    Kind = PackIconKind.Pause
                };
                StartAnimation();
            }
        } catch
        {
            // Ignored
        }
    }

    private void PreviewElement_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        if (Animation is null)
            return;

        try
        {
            using var @lock = Sync.EnterScope();

            if (Disposed)
                return;

            var canvas = e.Surface.Canvas;

            canvas.Clear(SKColors.DimGray);

            canvas.Scale(
                2.0f,
                2.0f,
                BODY_CENTER_X,
                BODY_CENTER_Y);

            // Apply horizontal flip for down and left directions
            if (CurrentViewDirection is ViewDirection.Down or ViewDirection.Left)
                canvas.Scale(
                    -1,
                    1,
                    BODY_CENTER_X,
                    BODY_CENTER_Y);

            // Draw body first
            DrawBodyFrame(canvas);

            // Draw effect on top
            DrawEffectFrame(canvas);
        } catch
        {
            // Ignored
        }
    }

    private void DrawBodyFrame(SKCanvas canvas)
    {
        if (BodyAnimation?.Frames is not { Count: > 0 } bodyFrames)
            return;

        // Use idle frame based on direction
        // Frame 0 is idle for up/left, Frame 5 is idle for down/right
        var idleFrameIndex = CurrentViewDirection is ViewDirection.Up or ViewDirection.Left ? 0 : 5;

        if (idleFrameIndex < bodyFrames.Count)
        {
            var bodyFrame = bodyFrames[idleFrameIndex];
            canvas.DrawImage(bodyFrame, 0, 0);
        }
    }

    private void DrawEffectFrame(SKCanvas canvas)
    {
        if ((CurrentFrameIndex < 0) || (CurrentFrameIndex >= Animation!.Frames.Count))
            return;

        var frame = Animation.Frames[CurrentFrameIndex];

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (frame is null)
            return;

        // Calculate position based on center point
        float frameCenterX,
              frameCenterY;

        if (IsEfa)
        {
            var efaFrame = EfaFile![CurrentFrameIndex];
            frameCenterX = efaFrame.CenterX;
            frameCenterY = efaFrame.CenterY;
        } else
        {
            var pt = CenterPoints![CurrentFrameIndex];
            frameCenterX = pt.X;
            frameCenterY = pt.Y;
        }

        // Position effect relative to body center
        var left = BODY_CENTER_X - frameCenterX;
        var top = BODY_CENTER_Y - frameCenterY;

        // Draw effect frame
        using var paint = new SKPaint();
        paint.BlendMode = SKBlendMode.SrcATop;

        canvas.DrawImage(
            frame,
            left,
            top,
            paint);

        // Draw debug bounds if enabled
        if (ShowBoundsChk?.IsChecked == true)
            DrawDebugBounds(canvas, left, top);
    }

    private void DrawDebugBounds(SKCanvas canvas, float left, float top)
    {
        if (IsEfa)
        {
            var efaFrame = EfaFile![CurrentFrameIndex];

            // Blue rectangle - image bounds
            using var imagePaint = new SKPaint();
            imagePaint.Color = SKColors.Blue;
            imagePaint.Style = SKPaintStyle.Stroke;
            imagePaint.StrokeWidth = 2;

            canvas.DrawRect(
                left,
                top,
                efaFrame.ImagePixelWidth,
                efaFrame.ImagePixelHeight,
                imagePaint);

            // Red rectangle - frame bounds
            using var framePaint = new SKPaint();
            framePaint.Color = SKColors.Red;
            framePaint.Style = SKPaintStyle.Stroke;
            framePaint.StrokeWidth = 1;

            canvas.DrawRect(
                left + efaFrame.Left,
                top + efaFrame.Top,
                efaFrame.FramePixelWidth,
                efaFrame.FramePixelHeight,
                framePaint);

            // Fuchsia circle - center point
            using var centerPaint = new SKPaint();
            centerPaint.Color = SKColors.Fuchsia;
            centerPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + efaFrame.CenterX,
                top + efaFrame.CenterY,
                2,
                centerPaint);

            // Yellow circle - top left point
            using var topLeftPaint = new SKPaint();
            topLeftPaint.Color = SKColors.Yellow;
            topLeftPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + efaFrame.Left,
                top + efaFrame.Top,
                2,
                topLeftPaint);
        } else
        {
            var epfFrame = EpfFile![CurrentFrameIndex];
            var centerPt = CenterPoints![CurrentFrameIndex];

            // Blue rectangle - image bounds
            using var imagePaint = new SKPaint();
            imagePaint.Color = SKColors.Blue;
            imagePaint.Style = SKPaintStyle.Stroke;
            imagePaint.StrokeWidth = 2;

            canvas.DrawRect(
                left,
                top,
                EpfFile.PixelWidth,
                EpfFile.PixelHeight,
                imagePaint);

            // Yellow rectangle - actual pixel data bounds
            using var pixelPaint = new SKPaint();
            pixelPaint.Color = SKColors.Yellow;
            pixelPaint.Style = SKPaintStyle.Stroke;
            pixelPaint.StrokeWidth = 1;

            canvas.DrawRect(
                left + epfFrame.Left,
                top + epfFrame.Top,
                epfFrame.PixelWidth,
                epfFrame.PixelHeight,
                pixelPaint);

            // Fuchsia circle - center point
            using var centerPaint = new SKPaint();
            centerPaint.Color = SKColors.Fuchsia;
            centerPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + centerPt.X,
                top + centerPt.Y,
                2,
                centerPaint);

            // Yellow circle - top left point
            using var topLeftPaint = new SKPaint();
            topLeftPaint.Color = SKColors.Yellow;
            topLeftPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + epfFrame.Left,
                top + epfFrame.Top,
                2,
                topLeftPaint);
        }
    }
    #endregion

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }
    #endregion
}