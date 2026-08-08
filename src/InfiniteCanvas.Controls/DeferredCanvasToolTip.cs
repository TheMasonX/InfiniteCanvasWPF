namespace InfiniteCanvas.Controls;

internal sealed class DeferredCanvasToolTip(string content)
{
    public string Content { get; } = content;

    public override string ToString() => Content;
}