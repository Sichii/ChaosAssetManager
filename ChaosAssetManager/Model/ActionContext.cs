using ChaosAssetManager.Controls;
using ChaosAssetManager.Definitions;
using ChaosAssetManager.ViewModel;
using SkiaSharp;

namespace ChaosAssetManager.Model;

public sealed class ActionContext
{
    public required ActionType ActionType { get; init; }
    public required TileGrabViewModel After { get; init; }
    public required TileGrabViewModel Before { get; init; }
    public required LayerFlags LayerFlags { get; init; }
    public required SKPoint TileCoordinates { get; init; }

    public void Redo(MapViewerViewModel viewModel)
    {
        switch (ActionType)
        {
            case ActionType.Draw:
            {
                MapEditorControl.Instance.ViewModel.TileGrab = After;
                After.Apply(viewModel, LayerFlags, TileCoordinates);

                break;
            }
            case ActionType.Erase:
            {
                MapEditorControl.Instance.ViewModel.TileGrab = After;
                After.Erase(viewModel, LayerFlags);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Undo(MapViewerViewModel viewModel)
    {
        MapEditorControl.Instance.ViewModel.TileGrab = Before;
        Before.Apply(viewModel, LayerFlags, TileCoordinates);
        MapEditorControl.Instance.ViewModel.TileGrab = After;
    }
}