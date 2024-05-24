using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace ChaosAssetManager.Controls;

public partial class EfaEditor : UserControl
{
    public EfaEditor() => InitializeComponent();

    private void FrameInvervalMsTbox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var c in e.Text)
            if (!char.IsDigit(c))
                e.Handled = true;
    }
}