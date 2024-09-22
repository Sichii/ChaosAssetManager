using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class EpfFileViewModel : INotifyPropertyChanged
{
    public EpfFrameViewModel this[int index]
    {
        get => new(EpfFile[index]);
        set => EpfFile[index] = value.EpfFrame;
    }

    public short PixelHeight
    {
        get => EpfFile.PixelHeight;

        set
        {
            if (EpfFile.PixelHeight == value)
                return;

            EpfFile.PixelHeight = value;
            OnPropertyChanged();
        }
    }

    public short PixelWidth
    {
        get => EpfFile.PixelWidth;

        set
        {
            if (EpfFile.PixelWidth == value)
                return;

            EpfFile.PixelWidth = value;
            OnPropertyChanged();
        }
    }

    public EpfFile EpfFile { get; }

    public EpfFileViewModel(EpfFile epfFile) => EpfFile = epfFile;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}