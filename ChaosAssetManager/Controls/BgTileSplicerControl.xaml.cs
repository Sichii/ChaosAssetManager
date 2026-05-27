using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ChaosAssetManager.Helpers;
using ChaosAssetManager.Model;
using DALib.Definitions;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ChaosAssetManager.Controls;

public sealed partial class BgTileSplicerControl : IDisposable
{
    private SKPoint GridOffset;
    private SKPoint GridOffsetAccumulator; //accumulates subpixel movement
    private bool IsDraggingGrid;
    private SKPoint LastGridDragPoint;
    private SKImage? SourceImage;
    private string? SourceImagePath;

    public BgTileSplicerControl() => InitializeComponent();

    public void Dispose()
    {
        SourceImage?.Dispose();
        SourceImage = null;

        Preview.Dispose();
    }

    private void BgTileSplicerControl_OnLoaded(object sender, RoutedEventArgs e)
    {
        //start with identity matrix (no transform)
        Preview.Matrix = SKMatrix.CreateIdentity();
        Preview.Redraw();
    }

    private void BrowseBtn_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select PNG Image to Splice",
            Filter = "PNG Images|*.png",
            Multiselect = false
        };

        if (!string.IsNullOrEmpty(PathHelper.Instance.BgTileSplicerFromPath))
            dialog.InitialDirectory = PathHelper.Instance.BgTileSplicerFromPath;

        if (dialog.ShowDialog() != true)
            return;

        //save path
        PathHelper.Instance.BgTileSplicerFromPath = Path.GetDirectoryName(dialog.FileName);
        PathHelper.Instance.Save();

        //dispose previous image
        SourceImage?.Dispose();

        //load new image
        SourceImage = SKImage.FromEncodedData(dialog.FileName);

        if (SourceImage == null)
        {
            Snackbar.MessageQueue!.Enqueue("Failed to load image");

            return;
        }

        //remember the full source path so the layout map can be written next to it
        SourceImagePath = dialog.FileName;

        //reset grid offset
        GridOffset = SKPoint.Empty;
        GridOffsetAccumulator = SKPoint.Empty;

        //fit and center the image in the preview
        FitImageToPreview();

        SendToImportBtn.IsEnabled = true;

        Preview.Redraw();
    }

    private void DrawIsometricGridOverlay(SKCanvas canvas, float canvasWidth, float canvasHeight)
    {
        if (SourceImage == null)
            return;

        //render grid to a bitmap at 1:1 scale with the image for crisp pixels
        using var gridBitmap = new SKBitmap(SourceImage.Width, SourceImage.Height);
        using var gridCanvas = new SKCanvas(gridBitmap);
        gridCanvas.Clear(SKColors.Transparent);

        const int TILE_WIDTH = CONSTANTS.TILE_WIDTH;
        const int TILE_HEIGHT = CONSTANTS.TILE_HEIGHT;
        const int HALF_TILE_WIDTH = CONSTANTS.HALF_TILE_WIDTH;
        const float HALF_TILE_HEIGHT = CONSTANTS.HALF_TILE_HEIGHT;

        //calculate the range of tile rows/columns that could intersect the image
        var minRow = (int)Math.Floor(-GridOffset.Y / HALF_TILE_HEIGHT) - 1;
        var maxRow = (int)Math.Ceiling((SourceImage.Height - GridOffset.Y) / HALF_TILE_HEIGHT) + 1;
        var minCol = (int)Math.Floor(-GridOffset.X / TILE_WIDTH) - 1;
        var maxCol = (int)Math.Ceiling((SourceImage.Width - GridOffset.X) / TILE_WIDTH) + 1;

        for (var row = minRow; row <= maxRow; row++)
        {
            for (var col = minCol; col <= maxCol; col++)
            {
                //calculate tile center position in grid coordinates
                float tileCenterX;

                if ((row % 2) == 0)
                    tileCenterX = col * TILE_WIDTH + HALF_TILE_WIDTH;
                else
                    tileCenterX = col * TILE_WIDTH;

                var tileCenterY = row * HALF_TILE_HEIGHT + HALF_TILE_HEIGHT;

                //convert to image coordinates (apply grid offset)
                var imgX = GridOffset.X + tileCenterX - HALF_TILE_WIDTH;
                var imgY = GridOffset.Y + tileCenterY - HALF_TILE_HEIGHT;

                //tile bounding box in image coordinates (rounded to pixels)
                var tileLeft = (int)Math.Round(imgX);
                var tileTop = (int)Math.Round(imgY);
                var tileRight = tileLeft + TILE_WIDTH;
                var tileBottom = tileTop + TILE_HEIGHT;

                //check if tile is fully within image bounds - skip partial tiles
                if ((tileLeft < 0) || (tileTop < 0) || (tileRight > SourceImage.Width) || (tileBottom > SourceImage.Height))
                    continue;

                //draw tile outline pixel by pixel
                RenderUtil.DrawTileOutline(
                    gridBitmap,
                    tileLeft,
                    tileTop,
                    SKColors.White);
            }
        }

        //draw the grid bitmap with difference blend mode for visibility on any background
        using var blendPaint = new SKPaint();
        blendPaint.BlendMode = SKBlendMode.Difference;

        canvas.DrawBitmap(
            gridBitmap,
            0,
            0,
            blendPaint);
    }

    private SplicedTileLayout ExtractTiles()
    {
        if (SourceImage == null)
            return new SplicedTileLayout([]);

        //collect kept tiles with positions + lattice coords, and empty-cell positions, separately
        var keptTiles = new List<(int tileLeft, int tileTop, int row, int col, SKImage image)>();
        var emptyCells = new List<(int tileLeft, int tileTop, int row, int col)>();

        const int TILE_WIDTH = CONSTANTS.TILE_WIDTH;
        const int TILE_HEIGHT = CONSTANTS.TILE_HEIGHT;
        const int HALF_TILE_WIDTH = CONSTANTS.HALF_TILE_WIDTH;
        const float HALF_TILE_HEIGHT = CONSTANTS.HALF_TILE_HEIGHT;

        //calculate the range of tile rows/columns that could intersect the image
        //grid origin is at GridOffset
        var minRow = (int)Math.Floor(-GridOffset.Y / HALF_TILE_HEIGHT) - 1;
        var maxRow = (int)Math.Ceiling((SourceImage.Height - GridOffset.Y) / HALF_TILE_HEIGHT) + 1;
        var minCol = (int)Math.Floor(-GridOffset.X / TILE_WIDTH) - 1;
        var maxCol = (int)Math.Ceiling((SourceImage.Width - GridOffset.X) / TILE_WIDTH) + 1;

        using var sourceBitmap = SKBitmap.FromImage(SourceImage);

        for (var row = minRow; row <= maxRow; row++)
        {
            for (var col = minCol; col <= maxCol; col++)
            {
                //calculate tile center position in grid coordinates
                float tileCenterX;

                if ((row % 2) == 0)
                    tileCenterX = col * TILE_WIDTH + HALF_TILE_WIDTH;
                else
                    tileCenterX = col * TILE_WIDTH;

                var tileCenterY = row * HALF_TILE_HEIGHT + HALF_TILE_HEIGHT;

                //convert to image coordinates (apply grid offset)
                var imgX = GridOffset.X + tileCenterX - HALF_TILE_WIDTH;
                var imgY = GridOffset.Y + tileCenterY - HALF_TILE_HEIGHT;

                //tile bounding box in image coordinates
                var tileLeft = (int)Math.Round(imgX);
                var tileTop = (int)Math.Round(imgY);
                var tileRight = tileLeft + TILE_WIDTH;
                var tileBottom = tileTop + TILE_HEIGHT;

                //check if tile is fully within image bounds - skip partial tiles
                if ((tileLeft < 0) || (tileTop < 0) || (tileRight > SourceImage.Width) || (tileBottom > SourceImage.Height))
                    continue;

                var tileRect = new SKRectI(
                    tileLeft,
                    tileTop,
                    tileRight,
                    tileBottom);

                //extract the tile with diamond mask
                using var tileBitmap = new SKBitmap(TILE_WIDTH, TILE_HEIGHT);
                using var tileCanvas = new SKCanvas(tileBitmap);

                tileCanvas.Clear(SKColors.Transparent);

                //manual pixel-by-pixel diamond clipping using shared row bounds calculation
                for (var py = 0; py < TILE_HEIGHT; py++)
                {
                    (var startX, var endX) = RenderUtil.GetTileRowBounds(py);

                    for (var px = startX; px <= endX; px++)
                    {
                        var srcX = tileRect.Left + px;
                        var srcY = tileRect.Top + py;

                        var color = sourceBitmap.GetPixel(srcX, srcY);
                        tileBitmap.SetPixel(px, py, color);
                    }
                }

                //record empty cells (transparent or black) so the map can show them as gaps
                if (IsTileEmpty(tileBitmap))
                {
                    emptyCells.Add((tileLeft, tileTop, row, col));

                    continue;
                }

                keptTiles.Add((tileLeft, tileTop, row, col, SKImage.FromBitmap(tileBitmap)));
            }
        }

        //assign placement order via the existing diagonal sort
        //"left to right, top to bottom" rotated 45 degrees: diagonal index = tileLeft / 2 + tileTop
        var orderedKept = keptTiles.OrderBy(tile => tile.tileLeft / 2 + tile.tileTop)
                                   .ThenBy(tile => tile.tileLeft)
                                   .ToList();

        var placedTiles = new List<PlacedTile>(orderedKept.Count + emptyCells.Count);

        for (var order = 0; order < orderedKept.Count; order++)
        {
            (var tileLeft, var tileTop, var row, var col, var image) = orderedKept[order];
            placedTiles.Add(new PlacedTile(tileLeft, tileTop, row, col, false, image, order));
        }

        foreach ((var tileLeft, var tileTop, var row, var col) in emptyCells)
            placedTiles.Add(new PlacedTile(tileLeft, tileTop, row, col, true, null, -1));

        return new SplicedTileLayout(placedTiles);
    }

    private void FitImageToPreview()
    {
        if (SourceImage == null)
            return;

        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();
        var previewWidth = (float)Preview.ActualWidth * dpiScale;
        var previewHeight = (float)Preview.ActualHeight * dpiScale;

        //handle case where preview hasn't been sized yet
        if ((previewWidth <= 0) || (previewHeight <= 0))
            return;

        //calculate scale to fit image in preview with some margin
        var scaleX = previewWidth / SourceImage.Width;
        var scaleY = previewHeight / SourceImage.Height;
        var scale = Math.Min(scaleX, scaleY) * 0.9f;

        //calculate translation to center the scaled image
        var scaledWidth = SourceImage.Width * scale;
        var scaledHeight = SourceImage.Height * scale;
        var translateX = (previewWidth - scaledWidth) / 2f;
        var translateY = (previewHeight - scaledHeight) / 2f;

        Preview.Matrix = SKMatrix.CreateScaleTranslation(
            scale,
            scale,
            translateX,
            translateY);
    }

    private static bool IsTileEmpty(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                //a pixel counts as content unless it is fully transparent or exact black
                if ((pixel.Alpha > 0) && ((pixel.Red != 0) || (pixel.Green != 0) || (pixel.Blue != 0)))
                    return false;
            }
        }

        return true;
    }

    private void Preview_OnPaint(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var canvasWidth = e.BackendRenderTarget.Width;
        var canvasHeight = e.BackendRenderTarget.Height;

        //draw source image at origin
        if (SourceImage != null)
        {
            canvas.DrawImage(SourceImage, 0, 0);

            //draw image bounds
            using var boundsPaint = new SKPaint
            {
                Color = new SKColor(
                    100,
                    100,
                    255,
                    100),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };

            canvas.DrawRect(
                0,
                0,
                SourceImage.Width,
                SourceImage.Height,
                boundsPaint);
        }

        //draw grid only for valid complete tiles
        if (SourceImage != null)
            DrawIsometricGridOverlay(canvas, canvasWidth, canvasHeight);
    }

    private void SendToImportBtn_OnClick(object sender, RoutedEventArgs e)
    {
        if (SourceImage == null)
        {
            Snackbar.MessageQueue!.Enqueue("No image loaded");

            return;
        }

        //extract tiles into a spatial layout
        var layout = ExtractTiles();
        var orderedImages = layout.OrderedImages;

        if (orderedImages.Count == 0)
        {
            Snackbar.MessageQueue!.Enqueue("No tiles could be extracted from the image");

            return;
        }

        //get MainWindow
        var mainWindow = Window.GetWindow(this) as MainWindow;

        if (mainWindow == null)
        {
            Snackbar.MessageQueue!.Enqueue("Could not access main window");

            return;
        }

        //render the layout map before handing the tile images to the importer
        //(the renderer reads the images but does not take ownership of them)
        using var mapImage = TileLayoutMapRenderer.Render(layout);

        //load the spliced tiles (TileImportControl now owns the ordered images)
        var tileImport = mainWindow.TileImportView;
        tileImport.LoadSplicedTiles(layout, SourceImagePath);

        //navigate to TileImport
        mainWindow.NavigateToTileImport();

        //write the layout map next to the source image and open it
        SaveAndOpenLayoutMap(mapImage);
    }

    private void SaveAndOpenLayoutMap(SKImage mapImage)
    {
        var fileName = string.IsNullOrEmpty(SourceImagePath)
            ? "tile_layout.png"
            : $"{Path.GetFileNameWithoutExtension(SourceImagePath)}_layout.png";

        var sourceDir = string.IsNullOrEmpty(SourceImagePath) ? null : Path.GetDirectoryName(SourceImagePath);

        //prefer writing next to the source image; fall back to temp if that isn't writable
        string? finalPath = null;

        if (!string.IsNullOrEmpty(sourceDir))
        {
            var candidate = Path.Combine(sourceDir, fileName);

            if (TryWritePng(mapImage, candidate))
                finalPath = candidate;
        }

        if (finalPath == null)
        {
            var tempCandidate = Path.Combine(Path.GetTempPath(), fileName);

            if (TryWritePng(mapImage, tempCandidate))
                finalPath = tempCandidate;
        }

        if (finalPath == null)
        {
            Snackbar.MessageQueue!.Enqueue("Failed to write the layout map");

            return;
        }

        //open in the system default image viewer
        try
        {
            Process.Start(
                new ProcessStartInfo(finalPath)
                {
                    UseShellExecute = true
                });
        } catch
        {
            //no viewer association - the file is still on disk
            Snackbar.MessageQueue!.Enqueue($"Layout map saved to {finalPath}");
        }
    }

    private static bool TryWritePng(SKImage image, string path)
    {
        try
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(path);
            data.SaveTo(stream);

            return true;
        } catch
        {
            //unwritable location (permissions, path too long) - caller falls back to temp
            return false;
        }
    }

    #region Grid Dragging
    private void Preview_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton != MouseButtonState.Pressed)
            return;

        IsDraggingGrid = true;
        var position = e.GetPosition(Preview);
        LastGridDragPoint = new SKPoint((float)position.X, (float)position.Y);

        e.Handled = true;
    }

    private void Preview_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsDraggingGrid || (e.RightButton != MouseButtonState.Pressed))
            return;

        var position = e.GetPosition(Preview);
        var dpiScale = (float)DpiHelper.GetDpiScaleFactor();

        //calculate delta in screen space
        var deltaX = (float)(position.X - LastGridDragPoint.X) * dpiScale;
        var deltaY = (float)(position.Y - LastGridDragPoint.Y) * dpiScale;

        //apply inverse of current view matrix scale to delta
        var scale = Preview.Matrix.ScaleX;

        //accumulate subpixel movement
        GridOffsetAccumulator = new SKPoint(GridOffsetAccumulator.X + deltaX / scale, GridOffsetAccumulator.Y + deltaY / scale);

        //snap GridOffset to whole pixels
        var snappedX = (float)Math.Round(GridOffsetAccumulator.X);
        var snappedY = (float)Math.Round(GridOffsetAccumulator.Y);

        GridOffset = new SKPoint(snappedX, snappedY);

        LastGridDragPoint = new SKPoint((float)position.X, (float)position.Y);

        Preview.Redraw();
        e.Handled = true;
    }

    private void Preview_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            IsDraggingGrid = false;
            GridOffsetAccumulator = GridOffset;
        }
    }
    #endregion
}