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

    public void Redo(MapViewerViewModel viewModel, MapEditorViewModel editorViewModel)
    {
        switch (ActionType)
        {
            case ActionType.Draw:
            {
                editorViewModel.TileGrab = After;
                After.Apply(viewModel, LayerFlags, TileCoordinates);

                break;
            }
            case ActionType.Erase:
            {
                editorViewModel.TileGrab = After;
                After.Erase(viewModel, LayerFlags);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Undo(MapViewerViewModel viewModel, MapEditorViewModel editorViewModel)
    {
        editorViewModel.TileGrab = Before;
        Before.Apply(viewModel, LayerFlags, TileCoordinates, true);
        editorViewModel.TileGrab = After;
    }
}