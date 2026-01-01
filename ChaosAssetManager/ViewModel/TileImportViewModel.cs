using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Definitions;
using SkiaSharp;

namespace ChaosAssetManager.ViewModel;

public sealed class TileImportViewModel : INotifyPropertyChanged, IDisposable
{
    private bool _isWall;
    private bool _isTransparent;

    public required SKImage Image { get; init; }
    public required string FileName { get; init; }
    public required bool ShowFlags { get; init; }

    public bool IsWall
    {
        get => _isWall;
        set
        {
            if (_isWall == value)
                return;

            _isWall = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Flags));
        }
    }

    public bool IsTransparent
    {
        get => _isTransparent;
        set
        {
            if (_isTransparent == value)
                return;

            _isTransparent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Flags));
        }
    }

    public TileFlags Flags => (IsWall ? TileFlags.Wall : TileFlags.None)
                            | (IsTransparent ? TileFlags.Transparent : TileFlags.None);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose() => Image.Dispose();
}
