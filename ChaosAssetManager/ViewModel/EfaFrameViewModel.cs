using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class EfaFrameViewModel : INotifyPropertyChanged
{
    public short CenterX
    {
        get => EfaFrame.CenterX;
        set
        {
            if (EfaFrame.CenterX == value)
                return;

            EfaFrame.CenterX = value;
            OnPropertyChanged();
        }
    }
    
    public short CenterY
    {
        get => EfaFrame.CenterY;
        set
        {
            if (EfaFrame.CenterY == value)
                return;

            EfaFrame.CenterY = value;
            OnPropertyChanged();
        }
    }
    
    public short FramePixelWidth
    {
        get => EfaFrame.FramePixelWidth;
        set
        {
            if (EfaFrame.FramePixelWidth == value)
                return;

            EfaFrame.FramePixelWidth = value;
            OnPropertyChanged();
        }
    }
    
    public short FramePixelHeight
    {
        get => EfaFrame.FramePixelHeight;
        set
        {
            if (EfaFrame.FramePixelHeight == value)
                return;

            EfaFrame.FramePixelHeight = value;
            OnPropertyChanged();
        }
    }
    
    public short ImagePixelWidth
    {
        get => EfaFrame.ImagePixelWidth;
        set
        {
            if (EfaFrame.ImagePixelWidth == value)
                return;

            EfaFrame.ImagePixelWidth = value;
            OnPropertyChanged();
        }
    }
    
    public short ImagePixelHeight
    {
        get => EfaFrame.ImagePixelHeight;
        set
        {
            if (EfaFrame.ImagePixelHeight == value)
                return;

            EfaFrame.ImagePixelHeight = value;
            OnPropertyChanged();
        }
    }
    
    public short Left
    {
        get => EfaFrame.Left;
        set
        {
            if (EfaFrame.Left == value)
                return;

            EfaFrame.Left = value;
            OnPropertyChanged();
        }
    }
    
    public short Top
    {
        get => EfaFrame.Top;
        set
        {
            if (EfaFrame.Top == value)
                return;

            EfaFrame.Top = value;
            OnPropertyChanged();
        }
    }
    
    public EfaFrame EfaFrame { get; }

    public EfaFrameViewModel(EfaFrame efaFrame) => EfaFrame = efaFrame;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}