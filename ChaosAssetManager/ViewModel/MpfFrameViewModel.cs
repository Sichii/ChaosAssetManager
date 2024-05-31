using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class MpfFrameViewModel : INotifyPropertyChanged
{
    public short Top
    {
        get => MpfFrame.Top;
        set
        {
            if (MpfFrame.Top == value)
                return;

            MpfFrame.Top = value;
            OnPropertyChanged();
        }
    }
    
    public short Left
    {
        get => MpfFrame.Left;
        set
        {
            if (MpfFrame.Left == value)
                return;

            MpfFrame.Left = value;
            OnPropertyChanged();
        }
    }
    
    public short Right
    {
        get => MpfFrame.Right;
        set
        {
            if (MpfFrame.Right == value)
                return;

            MpfFrame.Right = value;
            OnPropertyChanged();
        }
    }
    
    public short Bottom
    {
        get => MpfFrame.Bottom;
        set
        {
            if (MpfFrame.Bottom == value)
                return;

            MpfFrame.Bottom = value;
            OnPropertyChanged();
        }
    }
    
    public short CenterX
    {
        get => MpfFrame.CenterX;
        set
        {
            if (MpfFrame.CenterX == value)
                return;

            MpfFrame.CenterX = value;
            OnPropertyChanged();
        }
    }
    
    public short CenterY
    {
        get => MpfFrame.CenterY;
        set
        {
            if (MpfFrame.CenterY == value)
                return;

            MpfFrame.CenterY = value;
            OnPropertyChanged();
        }
    }
    
    public int PixelHeight => MpfFrame.PixelHeight;
    public int PixelWidth => MpfFrame.PixelWidth;
    
    public MpfFrame MpfFrame { get; }
    
    public MpfFrameViewModel(MpfFrame spfFrame) => MpfFrame = spfFrame;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}