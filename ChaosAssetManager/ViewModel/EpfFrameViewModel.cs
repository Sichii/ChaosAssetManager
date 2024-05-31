using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class EpfFrameViewModel : INotifyPropertyChanged
{
    public short Left
    {
        get => EpfFrame.Left;
        set
        {
            if (EpfFrame.Left == value)
                return;

            EpfFrame.Left = value;
            OnPropertyChanged();
        }
    }
    
    public short Top
    {
        get => EpfFrame.Top;
        set
        {
            if (EpfFrame.Top == value)
                return;

            EpfFrame.Top = value;
            OnPropertyChanged();
        }
    }
    
    public short Right
    {
        get => EpfFrame.Right;
        set
        {
            if (EpfFrame.Right == value)
                return;

            EpfFrame.Right = value;
            OnPropertyChanged();
        }
    }
    
    public short Bottom
    {
        get => EpfFrame.Bottom;
        set
        {
            if (EpfFrame.Bottom == value)
                return;

            EpfFrame.Bottom = value;
            OnPropertyChanged();
        }
    }

    public int PixelHeight => EpfFrame.PixelHeight;
    public int PixelWidth => EpfFrame.PixelWidth;

    public EpfFrame EpfFrame { get; }

    public EpfFrameViewModel(EpfFrame epfFrame) => EpfFrame = epfFrame;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}