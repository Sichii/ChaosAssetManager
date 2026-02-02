using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Button = System.Windows.Controls.Button;
using Palette = DALib.Drawing.Palette;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Controls;

public sealed partial class EpfEquipmentEditorControl : IDisposable, INotifyPropertyChanged
{
    //animation definitions: Name, Suffix, UpStart, UpEnd, RightStart, RightEnd

    //equipment types that render behind the body
    private static readonly HashSet<char> RenderBehindBodyTypes =
    [
        'f',
        'g'
    ]; //head2, accessories 2 //accessories 2

    private static readonly List<(string Name, string Suffix, int UpStart, int UpEnd, int RightStart, int RightEnd)> AnimationDefinitions =
    [
        //idle - uses 04 file if available, otherwise frame 0/5 from 01 file
        ("Idle", "04", 0, -1, -1, -1),

        //01 - walk (4 frames per direction)
        ("Walk", "01", 1, 4, 6, 9),

        //02 - assail
        ("Assail", "02", 0, 1, 2, 3),

        //03 - emotes
        ("Hands Up", "03", 0, 0, 1, 1),
        ("Blow Kiss", "03", 2, 3, 4, 5),
        ("Wave", "03", 6, 7, 8, 9),

        //b - priest/bard
        ("Priest Cast", "b", 0, 2, 3, 5),
        ("Bard Cast", "b", 6, 8, 9, 11),
        ("Perform", "b", 12, 12, 13, 13),

        //c - warrior
        ("Two-Handed Attack", "c", 0, 3, 4, 7),
        ("Jump Attack", "c", 8, 10, 11, 13),
        ("Swipe Attack", "c", 14, 15, 16, 17),
        ("Heavy Swipe Attack", "c", 18, 20, 21, 23),
        ("Heavy Jump Attack", "c", 24, 26, 27, 29),

        //d - monk
        ("Kick", "d", 0, 2, 3, 5),
        ("Punch", "d", 6, 7, 8, 9),
        ("Heavy Kick", "d", 10, 13, 14, 17),

        //e - rogue
        ("Stab", "e", 0, 1, 2, 3),
        ("Double Stab", "e", 4, 5, 6, 7),
        ("Bow Shot", "e", 8, 11, 12, 15),
        ("Heavy Bow Shot", "e", 16, 21, 22, 27),
        ("Volley", "e", 28, 31, 32, 35),

        //f - wizard
        ("Wizard Cast", "f", 0, 1, 2, 3),
        ("Summoner Cast", "f", 4, 7, 8, 11)
    ];

    private readonly Dictionary<string, EpfFile> EquipmentFiles;
    private readonly Palette EquipmentPalette;
    private readonly char EquipmentTypeLetter;
    private readonly Lock Sync = new();
    private CancellationTokenSource? AnimationCts;
    private PeriodicTimer? AnimationTimer;

    private Dictionary<string, Animation>? BodyAnimations;
    private Palette? BodyPalette;
    private (string Name, string Suffix, int UpStart, int UpEnd, int RightStart, int RightEnd)? CurrentAnimationDef;
    private int CurrentAnimationEndFrame;
    private int CurrentAnimationStartFrame;

    private string? CurrentAnimationSuffix;
    private Animation? CurrentBodyAnimation;
    private Animation? CurrentEquipmentAnimation;
    private int CurrentFrameIndex;
    private ViewDirection CurrentViewDirection = ViewDirection.Right;
    private bool Disposed;
    private Dictionary<string, Animation>? EquipmentAnimations;
    private bool IsPlaying;

    public EpfFrameViewModel? EpfFrameViewModel
    {
        get;
        set => SetField(ref field, value);
    }

    public bool IsMale { get; }

    public EpfEquipmentEditorControl(
        Dictionary<string, EpfFile> equipmentFiles,
        Palette equipmentPalette,
        char equipmentTypeLetter,
        bool isMale)
    {
        EquipmentFiles = equipmentFiles;
        EquipmentPalette = equipmentPalette;
        EquipmentTypeLetter = char.ToLower(equipmentTypeLetter);
        IsMale = isMale;

        InitializeComponent();

        //populate animation combo box with loaded equipment files
        PopulateAnimationComboBox();

        //load body animations
        LoadBodyAnimations();

        //render equipment animations
        RenderEquipmentAnimations();

        //select first animation
        if (AnimationCmb.Items.Count > 0)
            AnimationCmb.SelectedIndex = 0;
    }

    public void Dispose()
    {
        using var @lock = Sync.EnterScope();
        Disposed = true;

        AnimationCts?.Cancel();

        if (BodyAnimations is not null)
            foreach (var anim in BodyAnimations.Values)
                anim.Dispose();

        if (EquipmentAnimations is not null)
            foreach (var anim in EquipmentAnimations.Values)
                anim.Dispose();
    }

    private void LoadBodyAnimations()
    {
        if (string.IsNullOrEmpty(PathHelper.Instance.ArchivesPath))
            return;

        try
        {
            var archive = IsMale ? ArchiveCache.KhanMad : ArchiveCache.KhanWad;
            var palArchive = ArchiveCache.KhanPal;
            var palLookup = PaletteLookup.FromArchive("palb", palArchive);
            BodyPalette = palLookup.GetPaletteForId(1, IsMale ? KhanPalOverrideType.Male : KhanPalOverrideType.Female);

            //dispose old animations
            if (BodyAnimations is not null)
                foreach (var anim in BodyAnimations.Values)
                    anim.Dispose();

            BodyAnimations = new Dictionary<string, Animation>(StringComparer.OrdinalIgnoreCase);

            //load body animations that match our equipment files
            var genderPrefix = IsMale ? 'm' : 'w';

            foreach (var suffix in EquipmentFiles.Keys)
            {
                var bodyFileName = $"{genderPrefix}b001{suffix}.epf";

                if (!archive.Contains(bodyFileName))
                    continue;

                var bodyEpf = EpfFile.FromEntry(archive[bodyFileName]);
                var frames = bodyEpf.Select(frame => Graphics.RenderImage(frame, BodyPalette));
                BodyAnimations[suffix] = new Animation(new SKImageCollection(frames), 250);
            }
        } catch (Exception ex)
        {
            Snackbar.MessageQueue?.Enqueue($"Error loading body: {ex.Message}");
        }
    }

    private void PopulateAnimationComboBox()
    {
        AnimationCmb.Items.Clear();

        //only show animations for which we have loaded equipment files with all required frames
        foreach (var animDef in AnimationDefinitions)
        {
            //special handling for idle - can use 04 file OR fall back to 01 file
            if (animDef.Name == "Idle")
            {
                //check if 04 file exists
                if (EquipmentFiles.ContainsKey("04"))
                {
                    var item = new ComboBoxItem
                    {
                        Content = animDef.Name,
                        Tag = animDef
                    };
                    AnimationCmb.Items.Add(item);
                }

                //or if 01 file exists with at least 10 frames (for idle frame 0/5)
                else if (EquipmentFiles.TryGetValue("01", out var file01) && (file01.Count >= 10))
                {
                    //create a modified definition that uses 01 file with static idle frames
                    var idleDef = ("Idle", "01", 0, 0, 5, 5);

                    var item = new ComboBoxItem
                    {
                        Content = animDef.Name,
                        Tag = idleDef
                    };
                    AnimationCmb.Items.Add(item);
                }

                continue;
            }

            if (!EquipmentFiles.TryGetValue(animDef.Suffix, out var epfFile))
                continue;

            var frameCount = epfFile.Count;

            //calculate the maximum frame index required for this animation
            var maxRequiredFrame = animDef.RightEnd >= 0 ? animDef.RightEnd : 0;

            //only add the animation if we have enough frames
            if (frameCount >= maxRequiredFrame)
            {
                var item = new ComboBoxItem
                {
                    Content = animDef.Name,
                    Tag = animDef
                };
                AnimationCmb.Items.Add(item);
            }
        }
    }

    private void RenderEquipmentAnimations()
    {
        //dispose old animations
        if (EquipmentAnimations is not null)
            foreach (var anim in EquipmentAnimations.Values)
                anim.Dispose();

        EquipmentAnimations = new Dictionary<string, Animation>(StringComparer.OrdinalIgnoreCase);

        foreach ((var suffix, var epfFile) in EquipmentFiles)
        {
            var frames = epfFile.Select(frame => Graphics.RenderImage(frame, EquipmentPalette));
            EquipmentAnimations[suffix] = new Animation(new SKImageCollection(frames), 250);
        }
    }

    private void UpdateCurrentAnimation()
    {
        if (CurrentAnimationSuffix is null || CurrentAnimationDef is null)
            return;

        CurrentBodyAnimation = BodyAnimations?.GetValueOrDefault(CurrentAnimationSuffix);
        CurrentEquipmentAnimation = EquipmentAnimations?.GetValueOrDefault(CurrentAnimationSuffix);

        var def = CurrentAnimationDef.Value;
        var totalFrameCount = CurrentEquipmentAnimation?.Frames.Count ?? 0;

        //determine frame range based on view direction
        //up and left use the "up" frames, right and down use the "right" frames
        int startFrame,
            endFrame;

        if (CurrentViewDirection is ViewDirection.Up or ViewDirection.Left)
        {
            startFrame = def.UpStart;
            endFrame = def.UpEnd < 0 ? totalFrameCount / 2 - 1 : def.UpEnd;
        } else
        {
            startFrame = def.RightStart < 0 ? totalFrameCount / 2 : def.RightStart;
            endFrame = def.RightEnd < 0 ? totalFrameCount - 1 : def.RightEnd;
        }

        startFrame = Math.Max(0, Math.Min(startFrame, totalFrameCount - 1));
        endFrame = Math.Max(startFrame, Math.Min(endFrame, totalFrameCount - 1));

        CurrentAnimationStartFrame = startFrame;
        CurrentAnimationEndFrame = endFrame;

        //update frame list to only show frames in this animation's range
        var frameRange = Enumerable.Range(startFrame, endFrame - startFrame + 1)
                                   .ToList();
        FramesListView.ItemsSource = new CollectionView(frameRange);

        if (frameRange.Count > 0)
            FramesListView.SelectedIndex = 0;

        CurrentFrameIndex = startFrame;
        PreviewElement?.Redraw();
    }

    private void UpdateFrameViewModel()
    {
        if (CurrentAnimationSuffix is null || !EquipmentFiles.TryGetValue(CurrentAnimationSuffix, out var epfFile))
        {
            EpfFrameViewModel = null;

            return;
        }

        if ((CurrentFrameIndex >= 0) && (CurrentFrameIndex < epfFile.Count))
            EpfFrameViewModel = new EpfFrameViewModel(epfFile[CurrentFrameIndex]);
        else
            EpfFrameViewModel = null;
    }

    private enum ViewDirection
    {
        Up,
        Right,
        Down,
        Left
    }

    #region Event Handlers
    private void Direction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string dir })
        {
            var newDirection = dir switch
            {
                "Up"    => ViewDirection.Up,
                "Right" => ViewDirection.Right,
                "Down"  => ViewDirection.Down,
                "Left"  => ViewDirection.Left,
                _       => ViewDirection.Up
            };

            //if same direction and not playing, advance frame
            if ((newDirection == CurrentViewDirection) && !IsPlaying && (FramesListView.Items.Count > 0))
            {
                var nextIndex = (FramesListView.SelectedIndex + 1) % FramesListView.Items.Count;
                FramesListView.SelectedIndex = nextIndex;
            } else
            {
                CurrentViewDirection = newDirection;
                UpdateCurrentAnimation();
            }
        }
    }

    private void ShowBoundsChk_OnChecked(object sender, RoutedEventArgs e) => PreviewElement?.Redraw();

    private void Animation_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnimationCmb.SelectedItem is ComboBoxItem item
            && item.Tag is (string name, string suffix, int upStart, int upEnd, int rightStart, int rightEnd))
        {
            CurrentAnimationSuffix = suffix;
            CurrentAnimationDef = (name, suffix, upStart, upEnd, rightStart, rightEnd);
            UpdateCurrentAnimation();
        }
    }

    private void Frame_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FramesListView.SelectedItem is int frameIndex)
        {
            CurrentFrameIndex = frameIndex;
            UpdateFrameViewModel();
            PreviewElement?.Redraw();
        }
    }

    private void MoveUp_OnClick(object sender, RoutedEventArgs e) => MoveFrame(0, -1);
    private void MoveDown_OnClick(object sender, RoutedEventArgs e) => MoveFrame(0, 1);
    private void MoveLeft_OnClick(object sender, RoutedEventArgs e) => MoveFrame(-1, 0);
    private void MoveRight_OnClick(object sender, RoutedEventArgs e) => MoveFrame(1, 0);

    private void MoveFrame(int dx, int dy)
    {
        if (CurrentAnimationSuffix is null || !EquipmentFiles.TryGetValue(CurrentAnimationSuffix, out var epfFile))
            return;

        var moveAll = MoveAllFramesChk.IsChecked == true;
        var startIdx = moveAll ? CurrentAnimationStartFrame : CurrentFrameIndex;
        var endIdx = moveAll ? CurrentAnimationEndFrame : CurrentFrameIndex;

        for (var i = startIdx; i <= endIdx; i++)
        {
            if ((i < 0) || (i >= epfFile.Count))
                continue;

            var frame = epfFile[i];
            frame.Left = (short)(frame.Left + dx);
            frame.Right = (short)(frame.Right + dx);
            frame.Top = (short)(frame.Top + dy);
            frame.Bottom = (short)(frame.Bottom + dy);

            //re-render the equipment frame
            if ((EquipmentAnimations?.TryGetValue(CurrentAnimationSuffix, out var anim) == true) && (i < anim.Frames.Count))
            {
                anim.Frames[i]
                    .Dispose();
                anim.Frames[i] = Graphics.RenderImage(frame, EquipmentPalette);
            }
        }

        PreviewElement?.Redraw();
    }

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

        AnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
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
            while (!ct.IsCancellationRequested && AnimationTimer is not null)
            {
                await AnimationTimer.WaitForNextTickAsync(ct);

                var totalFrameCount = CurrentEquipmentAnimation?.Frames.Count ?? 0;
                var endFrame = CurrentAnimationEndFrame < 0 ? totalFrameCount - 1 : Math.Min(CurrentAnimationEndFrame, totalFrameCount - 1);
                var startFrame = Math.Min(CurrentAnimationStartFrame, endFrame);
                var frameCount = endFrame - startFrame + 1;

                if (frameCount > 0)
                {
                    //cycle within the animation's frame range
                    CurrentFrameIndex = startFrame + (CurrentFrameIndex - startFrame + 1) % frameCount;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        //find the index in the listview (which only contains frames in range)
                        var listIndex = CurrentFrameIndex - startFrame;

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

    private const int BODY_WIDTH = 57; //37;
    private const int BODY_HEIGHT = 85; //76;
    private const int BODY_CENTER_X = BODY_WIDTH / 2;
    private const int BODY_CENTER_Y = BODY_HEIGHT / 2;

    private (int X, int Y) GetEquipmentDrawOffset(EpfFrame epfFrame)
    {
        switch (EquipmentTypeLetter)
        {
            case 'c' or 'g':
                return (-27, 0);
            case 'w' or 'p':
                return (-27, 0);
            default:
                return (0, 0);
        }
    }

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
        } catch
        {
            //ignored
        }
    }

    private void PreviewElement_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        try
        {
            using var @lock = Sync.EnterScope();

            if (Disposed)
                return;

            var canvas = e.Surface.Canvas;
            var canvasWidth = e.Info.Width;
            var canvasHeight = e.Info.Height;

            canvas.Clear(SKColors.DimGray);

            // Draw grid before scaling (at 2x tile size so it matches the scaled sprites)
            RenderUtil.DrawIsometricGrid(canvas, canvasWidth, canvasHeight);

            // Scale around origin (body center is at 0,0)
            canvas.Scale(
                2.0f,
                2.0f,
                0,
                0);

            // Apply horizontal flip for down and left directions (flip around origin)
            if (CurrentViewDirection is ViewDirection.Down or ViewDirection.Left)
                canvas.Scale(
                    -1,
                    1,
                    0,
                    0);

            //determine render order based on equipment type
            var renderBehindBody = RenderBehindBodyTypes.Contains(EquipmentTypeLetter);

            if (renderBehindBody)
            {
                DrawEquipmentFrame(canvas);
                DrawBodyFrame(canvas);
            } else
            {
                DrawBodyFrame(canvas);
                DrawEquipmentFrame(canvas);
            }
        } catch
        {
            //ignored
        }
    }

    private void DrawBodyFrame(SKCanvas canvas)
    {
        //for "04" (idle animation), use idle frame from "01" body since there's no dedicated body animation
        if ((CurrentAnimationSuffix == "04") && CurrentBodyAnimation is null)
        {
            if (BodyAnimations?.GetValueOrDefault("01")
                              ?.Frames is { Count: > 0 } fallbackFrames)
            {
                //frame 0 is idle for up/left, frame 5 is idle for down/right
                var idleFrameIndex = CurrentViewDirection is ViewDirection.Up or ViewDirection.Left ? 0 : 5;

                if (idleFrameIndex < fallbackFrames.Count)
                {
                    var bodyFrame = fallbackFrames[idleFrameIndex];

                    // Draw body so that BODY_CENTER is at (0,0)
                    canvas.DrawImage(bodyFrame, -BODY_CENTER_X, -BODY_CENTER_Y);
                }
            }
        } else if (CurrentBodyAnimation?.Frames is { } bodyFrames && (CurrentFrameIndex >= 0) && (CurrentFrameIndex < bodyFrames.Count))
        {
            var bodyFrame = bodyFrames[CurrentFrameIndex];

            // Draw body so that BODY_CENTER is at (0,0)
            canvas.DrawImage(bodyFrame, -BODY_CENTER_X, -BODY_CENTER_Y);
        }
    }

    private void DrawEquipmentFrame(SKCanvas canvas)
    {
        if (CurrentEquipmentAnimation?.Frames is { } equipFrames
            && (CurrentFrameIndex >= 0)
            && (CurrentFrameIndex < equipFrames.Count)
            && CurrentAnimationSuffix is not null
            && EquipmentFiles.TryGetValue(CurrentAnimationSuffix, out var currentEpfFile)
            && (CurrentFrameIndex < currentEpfFile.Count))
        {
            var epfFrame = currentEpfFile[CurrentFrameIndex];
            (var typeOffsetX, var typeOffsetY) = GetEquipmentDrawOffset(epfFrame);
            var equipFrame = equipFrames[CurrentFrameIndex];

            //draw equipment relative to body center at (0,0)
            var offsetX = -BODY_CENTER_X + typeOffsetX;
            var offsetY = -BODY_CENTER_Y + typeOffsetY;

            //when left/top are negative, the rendered image has no padding
            //so we shift the draw position to compensate
            var drawX = offsetX + Math.Min(0, (int)epfFrame.Left);
            var drawY = offsetY + Math.Min(0, (int)epfFrame.Top);
            canvas.DrawImage(equipFrame, drawX, drawY);

            //draw debug rectangles if enabled
            if (ShowBoundsChk?.IsChecked == true)
            {
                //blue rectangle - full image bounds
                using var imagePaint = new SKPaint();
                imagePaint.Color = SKColors.Blue;
                imagePaint.Style = SKPaintStyle.Stroke;
                imagePaint.StrokeWidth = 2;

                canvas.DrawRect(
                    offsetX,
                    offsetY,
                    epfFrame.Right,
                    epfFrame.Bottom,
                    imagePaint);

                //yellow rectangle - actual pixel data bounds
                using var pixelPaint = new SKPaint();
                pixelPaint.Color = SKColors.Yellow;
                pixelPaint.Style = SKPaintStyle.Stroke;
                pixelPaint.StrokeWidth = 1;

                canvas.DrawRect(
                    offsetX + epfFrame.Left,
                    offsetY + epfFrame.Top,
                    epfFrame.PixelWidth,
                    epfFrame.PixelHeight,
                    pixelPaint);

                //yellow circle - top-left point
                using var topLeftPaint = new SKPaint();
                topLeftPaint.Color = SKColors.Yellow;
                topLeftPaint.Style = SKPaintStyle.Fill;

                canvas.DrawCircle(
                    offsetX + epfFrame.Left,
                    offsetY + epfFrame.Top,
                    2,
                    topLeftPaint);
            }
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