using System.Collections.ObjectModel;
using ChaosAssetManager.ViewModel;
using UserControl = System.Windows.Controls.UserControl;

namespace ChaosAssetManager.Controls;

public partial class MapEditorControl : UserControl
{
    public ObservableCollection<TileViewModel> Tiles { get; set; }
    
    public MapEditorControl() { InitializeComponent(); }
}