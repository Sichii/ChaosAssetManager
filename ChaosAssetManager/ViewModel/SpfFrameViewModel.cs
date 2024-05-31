using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class SpfFrameViewModel
{
    public ushort Top
    {
        get => SpfFrame.Top;
        set
        {
            if (SpfFrame.Top == value)
                return;

            SpfFrame.Top = value;
            OnPropertyChanged();
        }
    }
    
    public ushort Left
    {
        get => SpfFrame.Left;
        set
        {
            if (SpfFrame.Left == value)
                return;

            SpfFrame.Left = value;
            OnPropertyChanged();
        }
    }
    
    public ushort Right
    {
        get => SpfFrame.Right;
        set
        {
            if (SpfFrame.Right == value)
                return;

            SpfFrame.Right = value;
            OnPropertyChanged();
        }
    }
    
    public ushort Bottom
    {
        get => SpfFrame.Bottom;
        set
        {
            if (SpfFrame.Bottom == value)
                return;

            SpfFrame.Bottom = value;
            OnPropertyChanged();
        }
    }

    public int PixelHeight => SpfFrame.PixelHeight;
    public int PixelWidth => SpfFrame.PixelWidth;
    
    public SpfFrame SpfFrame { get; }
    
    public SpfFrameViewModel(SpfFrame spfFrame) => SpfFrame = spfFrame;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}