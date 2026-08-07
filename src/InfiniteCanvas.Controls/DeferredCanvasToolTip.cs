namespace InfiniteCanvas.Controls;

internal sealed class DeferredCanvasToolTip(string content)
{
    public override string ToString() => content;
}