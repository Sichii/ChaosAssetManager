using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
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

public sealed partial class NPCContentEditorControl : IDisposable, INotifyPropertyChanged
{
    private readonly Lock Sync = new();

    private Animation? Animation;
    private CancellationTokenSource? AnimationCts;
    private PeriodicTimer? AnimationTimer;
    private AnimationType CurrentAnimationType = AnimationType.Walk;
    private int CurrentFrameIndex;
    private ViewDirection CurrentViewDirection = ViewDirection.Right;
    private bool Disposed;
    private bool IsPlaying;

    public int SelectedFrameIndex
    {
        get;

        set
        {
            if (SetField(ref field, value) && (value >= 0))
                PreviewElement?.Redraw();
        }
    }

    public byte StopMotionRatio
    {
        get => MpfFile.OptionalAnimationRatio;

        set
        {
            if (MpfFile.OptionalAnimationRatio == value)
                return;

            MpfFile.OptionalAnimationRatio = value;
            OnPropertyChanged();
        }
    }

    public MpfFile MpfFile { get; }
    public Palette Palette { get; }

    public Visibility MultipleAttacksVisibility
        => MpfFile.FormatType == MpfFormatType.MultipleAttacks ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StopMotionVisibility => MpfFile.OptionalAnimationFrameCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public NPCContentEditorControl(MpfFile mpfFile, Palette palette)
    {
        MpfFile = mpfFile;
        Palette = palette;

        InitializeComponent();

        PopulateAnimationTypes();
        RenderAnimation();
    }

    public void Dispose()
    {
        using var @lock = Sync.EnterScope();
        Disposed = true;

        AnimationCts?.Cancel();
        AnimationTimer?.Dispose();
        Animation?.Dispose();
        PreviewElement?.Dispose();
    }

    private (int StartIndex, int Count) GetAnimationFrameRange()
    {
        //check if we need to fall back to idle frame (first walk frame per direction)
        var useIdleFallback = ((CurrentAnimationType == AnimationType.Standing) && (MpfFile.StandingFrameCount == 0))
                              || ((CurrentAnimationType == AnimationType.Optional) && (MpfFile.OptionalAnimationFrameCount == 0));

        if (useIdleFallback)
        {
            //idle frame: frame 0 for UP/LEFT, frame WalkFrameCount for RIGHT/DOWN
            var idleFrameIndex = CurrentViewDirection is ViewDirection.Up or ViewDirection.Left
                ? MpfFile.WalkFrameIndex
                : MpfFile.WalkFrameCount;

            return (idleFrameIndex, 1);
        }

        //get base range for animation type (count is per-direction, for UP only)
        (var baseIndex, var count) = CurrentAnimationType switch
        {
            AnimationType.Walk     => (MpfFile.WalkFrameIndex, MpfFile.WalkFrameCount),
            AnimationType.Attack   => (MpfFile.AttackFrameIndex, MpfFile.AttackFrameCount),
            AnimationType.Attack2  => (MpfFile.Attack2StartIndex, MpfFile.Attack2FrameCount),
            AnimationType.Attack3  => (MpfFile.Attack3StartIndex, MpfFile.Attack3FrameCount),
            AnimationType.Standing => (MpfFile.StandingFrameIndex, MpfFile.StandingFrameCount),
            AnimationType.Optional => (MpfFile.StandingFrameIndex, MpfFile.OptionalAnimationFrameCount),
            _                      => (0, 0)
        };

        //frames are organized: UP frames first, then RIGHT frames (same count each)
        //down/left are horizontal flips of right/up respectively
        //up/left use UP frames, right/down use RIGHT frames (offset by count)
        //for standing: if optional frames exist, the direction offset is OptionalAnimationFrameCount
        var dirOffsetAmount = (CurrentAnimationType == AnimationType.Standing) && (MpfFile.OptionalAnimationFrameCount > 0)
            ? MpfFile.OptionalAnimationFrameCount
            : count;
        var dirOffset = CurrentViewDirection is ViewDirection.Up or ViewDirection.Left ? 0 : dirOffsetAmount;

        return (baseIndex + dirOffset, count);
    }

    private void PopulateAnimationTypes()
    {
        AnimationTypeCmb.Items.Clear();

        AnimationTypeCmb.Items.Add(
            new ComboBoxItem
            {
                Content = "Walk",
                Tag = AnimationType.Walk
            });

        AnimationTypeCmb.Items.Add(
            new ComboBoxItem
            {
                Content = "Attack",
                Tag = AnimationType.Attack
            });

        if (MpfFile.FormatType == MpfFormatType.MultipleAttacks)
        {
            AnimationTypeCmb.Items.Add(
                new ComboBoxItem
                {
                    Content = "Attack 2",
                    Tag = AnimationType.Attack2
                });

            AnimationTypeCmb.Items.Add(
                new ComboBoxItem
                {
                    Content = "Attack 3",
                    Tag = AnimationType.Attack3
                });
        }

        AnimationTypeCmb.Items.Add(
            new ComboBoxItem
            {
                Content = "Standing",
                Tag = AnimationType.Standing
            });

        if (MpfFile.OptionalAnimationFrameCount > 0)
            AnimationTypeCmb.Items.Add(
                new ComboBoxItem
                {
                    Content = "Stop-motion",
                    Tag = AnimationType.Optional
                });

        //default to standing
        AnimationTypeCmb.SelectedItem = AnimationTypeCmb.Items
                                                        .OfType<ComboBoxItem>()
                                                        .FirstOrDefault(item => item.Tag is AnimationType.Standing);
    }

    private void RefreshFrameList()
    {
        if (FramesListView is null)
            return;

        (var startIndex, var count) = GetAnimationFrameRange();

        FramesListView.ItemsSource = Enumerable.Range(startIndex, count)
                                               .ToList();

        if (count > 0)
        {
            FramesListView.SelectedIndex = 0;

            //explicitly set frame indices in case SelectionChanged doesn't fire
            CurrentFrameIndex = startIndex;
            SelectedFrameIndex = startIndex;
        }

        PreviewElement?.Redraw();
    }

    private void RenderAnimation()
    {
        using var @lock = Sync.EnterScope();

        Animation?.Dispose();

        var frames = MpfFile.Select(frame => Graphics.RenderImage(frame, Palette));
        Animation = new Animation(new SKImageCollection(frames), 250);

        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
        CurrentFrameIndex = 0;

        RefreshFrameList();
    }

    private void ReRenderCurrentFrame()
    {
        using var @lock = Sync.EnterScope();

        if (Animation is null || (SelectedFrameIndex < 0) || (SelectedFrameIndex >= Animation.Frames.Count))
            return;

        Animation.Frames[SelectedFrameIndex]
                 .Dispose();
        Animation.Frames[SelectedFrameIndex] = Graphics.RenderImage(MpfFile[SelectedFrameIndex], Palette);

        PreviewElement?.Redraw();
    }

    private void ReRenderAllFrames()
    {
        using var @lock = Sync.EnterScope();

        if (Animation is null)
            return;

        for (var i = 0; i < Animation.Frames.Count; i++)
        {
            Animation.Frames[i]
                     .Dispose();
            Animation.Frames[i] = Graphics.RenderImage(MpfFile[i], Palette);
        }

        PreviewElement?.Redraw();
    }

    private enum AnimationType
    {
        Walk,
        Attack,
        Attack2,
        Attack3,
        Standing,
        Optional
    }

    private enum ViewDirection
    {
        Up,
        Right,
        Down,
        Left
    }

    #region File Properties
    public byte WalkFrameIndex
    {
        get => MpfFile.WalkFrameIndex;

        set
        {
            if (MpfFile.WalkFrameIndex == value)
                return;

            MpfFile.WalkFrameIndex = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte WalkFrameCount
    {
        get => MpfFile.WalkFrameCount;

        set
        {
            if (MpfFile.WalkFrameCount == value)
                return;

            MpfFile.WalkFrameCount = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte AttackFrameIndex
    {
        get => MpfFile.AttackFrameIndex;

        set
        {
            if (MpfFile.AttackFrameIndex == value)
                return;

            MpfFile.AttackFrameIndex = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte AttackFrameCount
    {
        get => MpfFile.AttackFrameCount;

        set
        {
            if (MpfFile.AttackFrameCount == value)
                return;

            MpfFile.AttackFrameCount = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte Attack2StartIndex
    {
        get => MpfFile.Attack2StartIndex;

        set
        {
            if (MpfFile.Attack2StartIndex == value)
                return;

            MpfFile.Attack2StartIndex = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte Attack2FrameCount
    {
        get => MpfFile.Attack2FrameCount;

        set
        {
            if (MpfFile.Attack2FrameCount == value)
                return;

            MpfFile.Attack2FrameCount = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte Attack3StartIndex
    {
        get => MpfFile.Attack3StartIndex;

        set
        {
            if (MpfFile.Attack3StartIndex == value)
                return;

            MpfFile.Attack3StartIndex = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte Attack3FrameCount
    {
        get => MpfFile.Attack3FrameCount;

        set
        {
            if (MpfFile.Attack3FrameCount == value)
                return;

            MpfFile.Attack3FrameCount = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte StandingFrameIndex
    {
        get => MpfFile.StandingFrameIndex;

        set
        {
            if (MpfFile.StandingFrameIndex == value)
                return;

            MpfFile.StandingFrameIndex = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte StandingFrameCount
    {
        get => MpfFile.StandingFrameCount;

        set
        {
            if (MpfFile.StandingFrameCount == value)
                return;

            MpfFile.StandingFrameCount = value;
            OnPropertyChanged();
            RefreshFrameList();
        }
    }

    public byte OptionalAnimationFrameCount
    {
        get => MpfFile.OptionalAnimationFrameCount;

        set
        {
            if (MpfFile.OptionalAnimationFrameCount == value)
                return;

            MpfFile.OptionalAnimationFrameCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StopMotionVisibility));
            RefreshFrameList();
        }
    }
    #endregion

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

        var framesToMove = moveAll ? MpfFile.ToList() : [MpfFile[SelectedFrameIndex]];

        foreach (var frame in framesToMove)
        {
            frame.Left = (short)(frame.Left + dx);
            frame.Top = (short)(frame.Top + dy);
            frame.Right = (short)(frame.Right + dx);
            frame.Bottom = (short)(frame.Bottom + dy);
        }

        if (moveAll)
            ReRenderAllFrames();
        else
            ReRenderCurrentFrame();
    }

    private void MoveCenterPoint(int dx, int dy)
    {
        if (SelectedFrameIndex < 0)
            return;

        var moveAll = MoveAllCentersChk.IsChecked == true;

        var framesToMove = moveAll ? MpfFile.ToList() : [MpfFile[SelectedFrameIndex]];

        foreach (var frame in framesToMove)
        {
            frame.CenterX = (short)(frame.CenterX + dx);
            frame.CenterY = (short)(frame.CenterY + dy);
        }

        PreviewElement?.Redraw();
    }
    #endregion

    #region Event Handlers
    private void Direction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string dir })
        {
            var newDirection = dir switch
            {
                "Up"    => ViewDirection.Up,
                "Right" => ViewDirection.Right,
                "Down"  => ViewDirection.Down,
                "Left"  => ViewDirection.Left,
                _       => ViewDirection.Right
            };

            //if same direction and not playing, advance frame
            if (newDirection == CurrentViewDirection && !IsPlaying && FramesListView.Items.Count > 0)
            {
                var nextIndex = (FramesListView.SelectedIndex + 1) % FramesListView.Items.Count;
                FramesListView.SelectedIndex = nextIndex;
            }
            else
            {
                CurrentViewDirection = newDirection;

                //refresh frame list since frames are direction-dependent
                RefreshFrameList();
            }
        }
    }

    private void AnimationType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnimationTypeCmb?.SelectedItem is ComboBoxItem { Tag: AnimationType type })
        {
            CurrentAnimationType = type;
            RefreshFrameList();
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

                (var startIndex, var count) = GetAnimationFrameRange();

                if (count > 0)
                {
                    //cycle through the current animation's frames
                    var relativeIndex = (CurrentFrameIndex - startIndex + 1) % count;
                    CurrentFrameIndex = startIndex + relativeIndex;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        //find the index in the list view
                        var listIndex = CurrentFrameIndex - startIndex;

                        if ((listIndex >= 0) && (listIndex < FramesListView.Items.Count))
                            FramesListView.SelectedIndex = listIndex;

                        PreviewElement?.Redraw();
                    });
                }
            }
        } catch (OperationCanceledException)
        {
            //expected when stopping
        }
    }
    #endregion

    #region Rendering
    private void PreviewElement_OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dpiScale = DpiHelper.GetDpiScaleFactor();
            var elementWidth = PreviewElement.ActualWidth * dpiScale;
            var elementHeight = PreviewElement.ActualHeight * dpiScale;

            var translateX = (float)(elementWidth / 2);
            var translateY = (float)(elementHeight / 2);

            PreviewElement.Matrix = SKMatrix.CreateTranslation(translateX, translateY);
            PreviewElement.Redraw();

            //auto-start animation if multiple frames
            (_, var count) = GetAnimationFrameRange();

            if ((count > 1) && !IsPlaying)
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
            //ignored
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
            var canvasWidth = e.Info.Width;
            var canvasHeight = e.Info.Height;

            canvas.Clear(SKColors.DimGray);

            //draw grid before scaling (at 2x tile size so it matches the scaled sprites)
            RenderUtil.DrawIsometricGrid(canvas, canvasWidth, canvasHeight);

            //scale around origin (NPC center is at 0,0)
            canvas.Scale(
                2.0f,
                2.0f,
                0,
                0);

            //apply horizontal flip for down and left directions (flip around origin)
            if (CurrentViewDirection is ViewDirection.Down or ViewDirection.Left)
                canvas.Scale(
                    -1,
                    1,
                    0,
                    0);

            //draw NPC frame
            DrawNPCFrame(canvas);
        } catch
        {
            //ignored
        }
    }

    private void DrawNPCFrame(SKCanvas canvas)
    {
        if ((CurrentFrameIndex < 0) || (CurrentFrameIndex >= Animation!.Frames.Count))
            return;

        var frame = Animation.Frames[CurrentFrameIndex];

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (frame is null)
            return;

        var mpfFrame = MpfFile[CurrentFrameIndex];

        //position NPC so that its center point is at (0,0)
        //when left/top are negative, the rendered image has no padding
        //so we shift the draw position to compensate
        var left = -mpfFrame.CenterX + Math.Min(0, (int)mpfFrame.Left);
        var top = -mpfFrame.CenterY + Math.Min(0, (int)mpfFrame.Top);

        //draw NPC frame
        canvas.DrawImage(frame, left, top);

        //draw debug bounds if enabled
        if (ShowBoundsChk?.IsChecked == true)
            DrawDebugBounds(
                canvas,
                -mpfFrame.CenterX,
                -mpfFrame.CenterY,
                mpfFrame);
    }

    private void DrawDebugBounds(
        SKCanvas canvas,
        float left,
        float top,
        MpfFrame mpfFrame)
    {
        //blue rectangle - image bounds
        using var imagePaint = new SKPaint();
        imagePaint.Color = SKColors.Blue;
        imagePaint.Style = SKPaintStyle.Stroke;
        imagePaint.StrokeWidth = 2;

        canvas.DrawRect(
            left,
            top,
            mpfFrame.Right,
            mpfFrame.Bottom,
            imagePaint);

        //yellow rectangle - frame bounds (Left/Top offset)
        using var framePaint = new SKPaint();
        framePaint.Color = SKColors.Yellow;
        framePaint.Style = SKPaintStyle.Stroke;
        framePaint.StrokeWidth = 1;

        canvas.DrawRect(
            left + mpfFrame.Left,
            top + mpfFrame.Top,
            mpfFrame.PixelWidth,
            mpfFrame.PixelHeight,
            framePaint);

        //fuchsia circle - center point
        using var centerPaint = new SKPaint();
        centerPaint.Color = SKColors.Fuchsia;
        centerPaint.Style = SKPaintStyle.Fill;

        canvas.DrawCircle(
            left + mpfFrame.CenterX,
            top + mpfFrame.CenterY,
            2,
            centerPaint);

        //yellow circle - top left point
        using var topLeftPaint = new SKPaint();
        topLeftPaint.Color = SKColors.Yellow;
        topLeftPaint.Style = SKPaintStyle.Fill;

        canvas.DrawCircle(
            left + mpfFrame.Left,
            top + mpfFrame.Top,
            2,
            topLeftPaint);
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