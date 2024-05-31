using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class HpfFileViewModel : INotifyPropertyChanged
{
    public int PixelHeight => HpfFile.PixelHeight;
    public int PixelWidth => HpfFile.PixelWidth;
    
    public HpfFile HpfFile { get; }
    
    public HpfFileViewModel(HpfFile spfFile) => HpfFile = spfFile;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}