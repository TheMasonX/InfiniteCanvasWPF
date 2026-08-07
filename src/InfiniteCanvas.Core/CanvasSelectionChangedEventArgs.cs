namespace InfiniteCanvas.Core;

public sealed class CanvasSelectionChangedEventArgs(ICanvasItem? selectedItem) : EventArgs
{
    public ICanvasItem? SelectedItem { get; } = selectedItem;
}