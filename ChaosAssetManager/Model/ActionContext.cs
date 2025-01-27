using ChaosAssetManager.Definitions;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Model;

public class ActionContext
{
    public required ActionType ActionType { get; init; }
    public required TileGrab After { get; init; }
    public required TileGrab Before { get; init; }
    public required LayerFlags LayerFlags { get; init; }

    public void Redo(MapViewerViewModel viewModel)
    {
        switch (ActionType)
        {
            case ActionType.Draw:
            {
                After.Apply(viewModel, LayerFlags);

                break;
            }
            case ActionType.Erase:
            {
                Before.Erase(viewModel, LayerFlags);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Undo(MapViewerViewModel viewModel) => Before.Apply(viewModel, LayerFlags);
}