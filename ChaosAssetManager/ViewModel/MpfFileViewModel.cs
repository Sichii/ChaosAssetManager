using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Definitions;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class MpfFileViewModel : INotifyPropertyChanged
{
    public byte AttackFrameCount
    {
        get => MpfFile.AttackFrameCount;
        set
        {
            if (MpfFile.AttackFrameCount == value)
                return;

            MpfFile.AttackFrameCount = value;
            OnPropertyChanged();
        }
    }
    
    public byte AttackFrameIndex
    {
        get => MpfFile.AttackFrameIndex;
        set
        {
            if (MpfFile.AttackFrameIndex == value)
                return;

            MpfFile.AttackFrameIndex = value;
            OnPropertyChanged();
        }
    }
    
    public byte Attack2FrameCount
    {
        get => MpfFile.Attack2FrameCount;
        set
        {
            if (MpfFile.Attack2FrameCount == value)
                return;

            MpfFile.Attack2FrameCount = value;
            OnPropertyChanged();
        }
    }
    
    public byte Attack2StartIndex
    {
        get => MpfFile.Attack2StartIndex;
        set
        {
            if (MpfFile.Attack2StartIndex == value)
                return;

            MpfFile.Attack2StartIndex = value;
            OnPropertyChanged();
        }
    }
    
    public byte Attack3FrameCount
    {
        get => MpfFile.Attack3FrameCount;
        set
        {
            if (MpfFile.Attack3FrameCount == value)
                return;

            MpfFile.Attack3FrameCount = value;
            OnPropertyChanged();
        }
    }
    
    public byte Attack3StartIndex
    {
        get => MpfFile.Attack3StartIndex;
        set
        {
            if (MpfFile.Attack3StartIndex == value)
                return;

            MpfFile.Attack3StartIndex = value;
            OnPropertyChanged();
        }
    }
    
    public int PaletteNumber
    {
        get => MpfFile.PaletteNumber;
        set
        {
            if (MpfFile.PaletteNumber == value)
                return;

            MpfFile.PaletteNumber = value;
            OnPropertyChanged();
        }
    }

    public short PixelHeight
    {
        get => MpfFile.PixelHeight;
        set
        {
            if (MpfFile.PixelHeight == value)
                return;

            MpfFile.PixelHeight = value;
            OnPropertyChanged();
        }
    }
    
    public short PixelWidth
    {
        get => MpfFile.PixelWidth;
        set
        {
            if (MpfFile.PixelWidth == value)
                return;

            MpfFile.PixelWidth = value;
            OnPropertyChanged();
        }
    }
    
    public byte StandingFrameCount
    {
        get => MpfFile.StandingFrameCount;
        set
        {
            if (MpfFile.StandingFrameCount == value)
                return;

            MpfFile.StandingFrameCount = value;
            OnPropertyChanged();
        }
    }
    
    public byte StandingFrameIndex
    {
        get => MpfFile.StandingFrameIndex;
        set
        {
            if (MpfFile.StandingFrameIndex == value)
                return;

            MpfFile.StandingFrameIndex = value;
            OnPropertyChanged();
        }
    }
    
    public byte StopMotionFailureRatio
    {
        get => MpfFile.StopMotionFailureRatio;
        set
        {
            if (MpfFile.StopMotionFailureRatio == value)
                return;

            MpfFile.StopMotionFailureRatio = value;
            OnPropertyChanged();
        }
    }
    
    public byte StopMotionFrameCount
    {
        get => MpfFile.StopMotionFrameCount;
        set
        {
            if (MpfFile.StopMotionFrameCount == value)
                return;

            MpfFile.StopMotionFrameCount = value;
            OnPropertyChanged();
        }
    }
    
    public byte WalkFrameCount
    {
        get => MpfFile.WalkFrameCount;
        set
        {
            if (MpfFile.WalkFrameCount == value)
                return;

            MpfFile.WalkFrameCount = value;
            OnPropertyChanged();
        }
    }
    
    public byte WalkFrameIndex
    {
        get => MpfFile.WalkFrameIndex;
        set
        {
            if (MpfFile.WalkFrameIndex == value)
                return;

            MpfFile.WalkFrameIndex = value;
            OnPropertyChanged();
        }
    }
    
    public MpfFormatType FormatType
    {
        get => MpfFile.FormatType;
        set
        {
            if (MpfFile.FormatType == value)
                return;

            MpfFile.FormatType = value;
            OnPropertyChanged();
        }
    }

    public MpfFile MpfFile { get; }
    
    public MpfFileViewModel(MpfFile spfFile) => MpfFile = spfFile;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}