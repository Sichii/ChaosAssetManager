using System.ComponentModel;
using System.Runtime.CompilerServices;
using DALib.Drawing;

namespace ChaosAssetManager.ViewModel;

public class SpfFileViewModel : INotifyPropertyChanged
{
    public SpfFile SpfFile { get; }
    
    public SpfFileViewModel(SpfFile spfFile) => SpfFile = spfFile;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}