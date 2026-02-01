using System.ComponentModel;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace ChaosAssetManager.ViewModel;

public sealed class PanelSpriteViewModel : INotifyPropertyChanged, IDisposable
{
    private bool _isSelected;

    /// <summary>
    ///     1-based global sprite id
    /// </summary>
    public required int GlobalId { get; init; }

    /// <summary>
    ///     0-based slot index within the page
    /// </summary>
    public required int SlotIndex { get; init; }

    /// <summary>
    ///     The rendered sprite image
    /// </summary>
    public required SKImage? Image { get; init; }

    /// <summary>
    ///     Whether this slot contains a non-empty sprite
    /// </summary>
    public required bool IsEmpty { get; init; }

    public bool IsSelected
    {
        get => _isSelected;

        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose() => Image?.Dispose();
}
