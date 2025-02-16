using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using ChaosAssetManager.ViewModel;
using DALib.Definitions;
using DALib.Drawing;
using DALib.Utility;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Graphics = DALib.Drawing.Graphics;

namespace ChaosAssetManager.Controls;

public partial class EpfEquipmentEditorControl : IDisposable, INotifyPropertyChanged
{
    private readonly Dictionary<string, Animation> Animations;
    private readonly Palette Palette;
    private readonly Lock Sync;
    private Animation? Animation;
    private int CurrentFrameIndex;
    private bool Disposed;
    private Task? ImageAnimationTask;
    private PeriodicTimer? ImageAnimationTimer;

    public int CurrentCenterX { get; set; }
    public int CurrentCenterY { get; set; }

    public EpfFileViewModel EpfFileViewModel
    {
        get;
        set => SetField(ref field, value);
    }

    public EpfFrameViewModel? EpfFrameViewModel
    {
        get;

        set
        {
            SetField(ref field, value);

            if (field is not null)
                RenderFramePreview();
        }
    }

    public bool IsMale
    {
        get;

        set
        {
            SetField(ref field, value);
            PopulateAnimations();
        }
    }

    public int SelectedFrameIndex
    {
        get;

        set
        {
            SetField(ref field, value);
            EpfFrameViewModel = value >= 0 ? EpfFileViewModel[value] : null;
            OnPropertyChanged(nameof(CurrentCenterX));
            OnPropertyChanged(nameof(CurrentCenterY));
        }
    }

    public EpfEquipmentEditorControl(EpfFile epfImage, Palette palette)
    {
        Sync = new Lock();

        InitializeComponent();

        Palette = palette;
        Animations = new Dictionary<string, Animation>(StringComparer.OrdinalIgnoreCase);
        FramesListView.ItemsSource = new CollectionView(Enumerable.Range(0, epfImage.Count));
        EpfFileViewModel = new EpfFileViewModel(epfImage);
        SelectedFrameIndex = 0;

        RenderImagePreview();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.EnterScope();

        Animation?.Dispose();
        Disposed = true;
    }

    private void Load_OnClick(object sender, RoutedEventArgs e) => throw new NotImplementedException();

    private void PopulateAnimations()
    {
        var archiveName = $"khan{(IsMale ? "m" : "w")}ad";
        var archive = ArchiveCache.GetArchive(PathHelper.Instance.ArchivesPath!, archiveName);
        var palArchive = ArchiveCache.GetArchive(PathHelper.Instance.ArchivesPath!, "khanpal");
        var palLookup = PaletteLookup.FromArchive("palb", palArchive);
        var palette = palLookup.GetPaletteForId(1, IsMale ? KhanPalOverrideType.Male : KhanPalOverrideType.Female);

        var walkEpf = EpfFile.FromArchive("mb00101.epf", archive);
        var assailEpf = EpfFile.FromArchive("mb00102.epf", archive);
        var emoteEpf = EpfFile.FromArchive("mb00103.epf", archive);
        var priestEpf = EpfFile.FromArchive("mb001b.epf", archive);
        var warriorEpf = EpfFile.FromArchive("mb001c.epf", archive);
        var monkEpf = EpfFile.FromArchive("mb001d.epf", archive);
        var rogueEpf = EpfFile.FromArchive("mb001e.epf", archive);
        var wizardEpf = EpfFile.FromArchive("mb001f.epf", archive);
        var interval = 250;

        var walkAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = walkEpf,
                Palette = palette
            });

        Animations["Up Idle"] = new Animation(new SKImageCollection(walkAnimation.Frames[..0]), interval);
        Animations["Down Idle"] = new Animation(new SKImageCollection(walkAnimation.Frames[5..5]), interval);
        Animations["Up Walk"] = new Animation(new SKImageCollection(walkAnimation.Frames[..4]), interval);
        Animations["Down Walk"] = new Animation(new SKImageCollection(walkAnimation.Frames[5..9]), interval);

        var assailAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = assailEpf,
                Palette = palette
            });

        Animations["Up Assail"] = new Animation(new SKImageCollection(assailAnimation.Frames[..1]), interval);
        Animations["Down Assail"] = new Animation(new SKImageCollection(assailAnimation.Frames[2..3]), interval);

        var emoteAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = emoteEpf,
                Palette = palette
            });

        Animations["Up HandsUp"] = new Animation(new SKImageCollection(emoteAnimation.Frames[..0]), interval);
        Animations["Down HandsUp"] = new Animation(new SKImageCollection(emoteAnimation.Frames[1..1]), interval);
        Animations["Up BlowKiss"] = new Animation(new SKImageCollection(emoteAnimation.Frames[2..3]), interval);
        Animations["Down BlowKiss"] = new Animation(new SKImageCollection(emoteAnimation.Frames[4..5]), interval);
        Animations["Down Wave"] = new Animation(new SKImageCollection(emoteAnimation.Frames[6..7]), interval);
        Animations["Up Wave"] = new Animation(new SKImageCollection(emoteAnimation.Frames[8..9]), interval);

        var priestAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = priestEpf,
                Palette = palette
            });

        Animations["Up PriestCast"] = new Animation(new SKImageCollection(priestAnimation.Frames[..2]), interval);
        Animations["Down PriestCast"] = new Animation(new SKImageCollection(priestAnimation.Frames[3..5]), interval);
        Animations["Up BardAssail"] = new Animation(new SKImageCollection(priestAnimation.Frames[6..8]), interval);
        Animations["Down BardAssail"] = new Animation(new SKImageCollection(priestAnimation.Frames[9..11]), interval);
        Animations["Up BardCast"] = new Animation(new SKImageCollection(priestAnimation.Frames[12..12]), interval);
        Animations["Down BardCast"] = new Animation(new SKImageCollection(priestAnimation.Frames[13..13]), interval);

        var warriorAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = warriorEpf,
                Palette = palette
            });

        Animations["Up TwoHandedAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[..3]), interval);
        Animations["Down TwoHandedAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[4..7]), interval);
        Animations["Up JumpAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[8..10]), interval);
        Animations["Down JumpAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[11..13]), interval);
        Animations["Up SwipeAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[14..15]), interval);
        Animations["Down SwipeAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[16..17]), interval);
        Animations["Up HeavySwipeAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[18..20]), interval);
        Animations["Down HeavySwipeAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[21..23]), interval);
        Animations["Up HeavyJumpAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[24..26]), interval);
        Animations["Down HeavyJumpAttack"] = new Animation(new SKImageCollection(warriorAnimation.Frames[27..29]), interval);

        var monkAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = monkEpf,
                Palette = palette
            });

        Animations["Up KickAttack"] = new Animation(new SKImageCollection(monkAnimation.Frames[..2]), interval);
        Animations["Down KickAttack"] = new Animation(new SKImageCollection(monkAnimation.Frames[3..5]), interval);
        Animations["Up PunchAttack"] = new Animation(new SKImageCollection(monkAnimation.Frames[6..7]), interval);
        Animations["Down PunchAttack"] = new Animation(new SKImageCollection(monkAnimation.Frames[8..9]), interval);
        Animations["Up HeavyKickAttack"] = new Animation(new SKImageCollection(monkAnimation.Frames[10..13]), interval);
        Animations["Down HeavyKickAttack"] = new Animation(new SKImageCollection(monkAnimation.Frames[14..17]), interval);

        var rogueAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = rogueEpf,
                Palette = palette
            });

        Animations["Up StabAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[..1]), interval);
        Animations["Down StabAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[2..3]), interval);
        Animations["Up DoubleStabAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[4..5]), interval);
        Animations["Down DoubleStabAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[6..7]), interval);
        Animations["Up ArrowShotAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[8..11]), interval);
        Animations["Down ArrowShotAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[12..15]), interval);
        Animations["Up HeavyArrowShotAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[16..21]), interval);
        Animations["Down HeavyArrowShotAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[22..27]), interval);
        Animations["Up FarArrowShotAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[28..31]), interval);
        Animations["Down FarArrowShotAttack"] = new Animation(new SKImageCollection(rogueAnimation.Frames[32..35]), interval);

        var wizardAnimation = Transform(
            new Palettized<EpfFile>
            {
                Entity = wizardEpf,
                Palette = palette
            });

        Animations["Up WizardCast"] = new Animation(new SKImageCollection(wizardAnimation.Frames[..1]), interval);
        Animations["Down WizardCast"] = new Animation(new SKImageCollection(wizardAnimation.Frames[2..3]), interval);
        Animations["Up SummonerCast"] = new Animation(new SKImageCollection(wizardAnimation.Frames[4..7]), interval);
        Animations["Down SummonerCast"] = new Animation(new SKImageCollection(wizardAnimation.Frames[8..11]), interval);

        return;

        static Animation Transform(Palettized<EpfFile> epfFile)
        {
            var transformer = epfFile.Entity.Select(frame => Graphics.RenderImage(frame, epfFile.Palette));
            var images = new SKImageCollection(transformer);

            return new Animation(images, 250);
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e) => throw new NotImplementedException();

    private async Task UpdateLoop()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / 30d));
        var waitingForArchivesDirectory = true;

        Snackbar.MessageQueue!.Enqueue(
            "Set Archives Directory in options (Gear icon)",
            null,
            null,
            null,
            false,
            true,
            TimeSpan.FromHours(24));

        while (true)
            try
            {
                await timer.WaitForNextTickAsync();

                if (!string.IsNullOrEmpty(PathHelper.Instance.ArchivesPath)
                    && PathHelper.ArchivePathIsValid(PathHelper.Instance.ArchivesPath)
                    && waitingForArchivesDirectory)
                {
                    Snackbar.MessageQueue.Clear();
                    waitingForArchivesDirectory = false;

                    //var archive = ArchiveCache.GetArchive(PathHelper.Instance.ArchivesPath, "");
                    //BgImage = null;
                }
            } catch
            {
                //ignored
            }
    }

    #region NotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // ReSharper disable once UnusedMethodReturnValue.Local
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }
    #endregion

    #region Frame Events
    private void FrameApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        if (SelectedFrameIndex < 0)
            return;

        var frame = EpfFrameViewModel;

        if (frame == null)
            return;

        var expectedLength = frame.PixelWidth * frame.PixelHeight;

        if (expectedLength > frame.EpfFrame.Data.Length)
        {
            Snackbar.MessageQueue!.Enqueue(
                "The width or height of the image are higher than expected. Width x Height must be less than or equal to the image data length.");

            return;
        }

        Animation!.Frames[SelectedFrameIndex]
                  .Dispose();
        Animation!.Frames[SelectedFrameIndex] = Graphics.RenderImage(frame.EpfFrame, Palette);

        RenderFramePreview();
    }

    private void RenderFramePreview()
    {
        using var @lock = Sync.EnterScope();

        if (SelectedFrameIndex < 0)
            return;

        FrameRenderElement.Redraw();
    }

    private void FrameRenderElement_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        if (SelectedFrameIndex < 0)
            return;

        try
        {
            using var @lock = Sync.EnterScope();

            if (Disposed)
                return;

            var frame = Animation!.Frames[SelectedFrameIndex];
            var canvas = e.Surface.Canvas;
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var imageScale = 1.5f / dpiScale;
            var centerX = 100 / 2f / imageScale;
            var centerY = 200 / 2f / imageScale;

            // sometimes frames can be null
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (frame is null)
                return;

            // draw the background image without additional scaling
            //canvas.DrawImage(BgImage, 0, 0);

            var epfFrame = EpfFrameViewModel!.EpfFrame;

            // draw the top image in the center
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.SrcATop;

            canvas.Scale(
                2.0f * dpiScale,
                2.0f * dpiScale,
                centerX,
                centerY);

            var frameCenterX = CurrentCenterX;
            var frameCenterY = CurrentCenterY;
            var left = centerX - frameCenterX - 2.17f;
            var top = centerY - frameCenterY + 33.66f;

            canvas.DrawImage(
                frame,
                left,
                top,
                paint);

            // Draw the image rectangle
            using var imagePaint = new SKPaint();
            imagePaint.Color = SKColors.Blue;
            imagePaint.Style = SKPaintStyle.Stroke;
            imagePaint.StrokeWidth = 2;

            canvas.DrawRect(
                left,
                top,
                EpfFileViewModel.PixelWidth,
                EpfFileViewModel.PixelHeight,
                imagePaint);

            // Draw the center point
            using var centerPaint = new SKPaint();
            centerPaint.Color = SKColors.Fuchsia;
            centerPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + frameCenterX,
                top + frameCenterY,
                2,
                centerPaint);

            // Draw the top left point
            using var topLeftPaint = new SKPaint();
            topLeftPaint.Color = SKColors.Yellow;
            topLeftPaint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(
                left + epfFrame.Left,
                top + epfFrame.Top,
                2,
                topLeftPaint);

            // Draw image bytes rect
            using var imageBytesPaint = new SKPaint();
            imageBytesPaint.Color = SKColors.Yellow;
            imageBytesPaint.Style = SKPaintStyle.Stroke;
            imageBytesPaint.StrokeWidth = 1;

            canvas.DrawRect(
                left + epfFrame.Left,
                top + epfFrame.Top,
                epfFrame.PixelWidth,
                epfFrame.PixelHeight,
                imageBytesPaint);
        } catch
        {
            //ignored
        }
    }

    private void FrameRenderElement_OnElementLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var elementWidth = FrameRenderElement.ActualWidth * dpiScale;
            var elementHeight = FrameRenderElement.ActualHeight * dpiScale;
            var imageScale = 1.5f / dpiScale;

            //calculate the translation to center the image
            var translateX = (float)(elementWidth - 100 / imageScale) / 2f;
            var translateY = (float)(elementHeight - 200 / imageScale) / 2f;

            //center the image
            FrameRenderElement.Matrix = SKMatrix.CreateTranslation(translateX, translateY);
            FrameRenderElement.Redraw();
        } catch
        {
            //ignored
        }
    }
    #endregion

    #region Image Events
    private void ImageApplyBtn_OnClick(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        RenderImagePreview();
    }

    private async Task AnimateElement()
    {
        ArgumentNullException.ThrowIfNull(Animation);
        ArgumentNullException.ThrowIfNull(ImageAnimationTimer);

        try
        {
            while (true)
            {
                await ImageAnimationTimer.WaitForNextTickAsync();

                using var @lock = Sync.EnterScope();

                if (Disposed)
                    return;

                CurrentFrameIndex = (CurrentFrameIndex + 1) % Animation.Frames.Count;
                ImageRenderElement.Redraw();
            }
        } catch
        {
            //ignored
        }
    }

    private void RenderImagePreview()
    {
        using var @lock = Sync.EnterScope();

        Animation?.Dispose();

        var transformer = EpfFileViewModel.EpfFile.Select(frame => Graphics.RenderImage(frame, Palette));
        var images = new SKImageCollection(transformer);
        Animation = new Animation(images);
        ImageAnimationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Animation.FrameIntervalMs));
        CurrentFrameIndex = 0;
    }

    private void ImageRenderElement_OnElementLoaded(object? sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var elementWidth = ImageRenderElement.ActualWidth * dpiScale;
            var elementHeight = ImageRenderElement.ActualHeight * dpiScale;
            var imageScale = 1.5f / dpiScale;

            //calculate the translation to center the image
            var translateX = (float)(elementWidth - 100 / imageScale) / 2f;
            var translateY = (float)(elementHeight - 200 / imageScale) / 2f;

            //center the image
            ImageRenderElement.Matrix = SKMatrix.CreateTranslation(translateX, translateY);
            ImageRenderElement.Redraw();

            if (Animation.Frames.Count > 1)
                ImageAnimationTask ??= AnimateElement();
        } catch
        {
            //ignored
        }
    }

    private void ImageRenderElement_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(Animation);

        try
        {
            using var @lock = Sync.EnterScope();

            if (Disposed)
                return;

            var canvas = e.Surface.Canvas;
            var frame = Animation.Frames[CurrentFrameIndex];
            var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
            var imageScale = 1.5f / dpiScale;
            var centerX = 100 / 2f / imageScale;
            var centerY = 200 / 2f / imageScale;

            // Sometimes frames can be null
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (frame is null)
                return;

            var frameCenterX = CurrentCenterX;
            var frameCenterY = CurrentCenterY;
            var left = centerX - frameCenterX - 2.17f;
            var top = centerY - frameCenterY + 33.66f;

            // Draw the top image in the center
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.SrcATop;

            canvas.Scale(
                2.0f * dpiScale,
                2.0f * dpiScale,
                centerX,
                centerY);

            canvas.DrawImage(
                frame,
                left,
                top,
                paint);
        } catch
        {
            //ignored
        }
    }
    #endregion
}