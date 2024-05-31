using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Definitions;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class EfaFileViewModel : INotifyPropertyChanged
{
    public EfaBlendingType BlendingType
    {
        get => EfaFile.BlendingType;
        set
        {
            if (EfaFile.BlendingType == value)
                return;

            EfaFile.BlendingType = value;
            OnPropertyChanged();
        }
    }
    
    public int FrameIntervalMs
    {
        get => EfaFile.FrameIntervalMs;
        set
        {
            if (EfaFile.FrameIntervalMs == value)
                return;

            EfaFile.FrameIntervalMs = value;
            OnPropertyChanged();
        }
    }

    public EfaFile EfaFile { get; }

    public EfaFileViewModel(EfaFile efaFile) => EfaFile = efaFile;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}