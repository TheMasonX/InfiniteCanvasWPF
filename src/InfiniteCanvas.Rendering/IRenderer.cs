namespace InfiniteCanvas.Rendering;

public interface IRenderer<in TScene, out TOutput>
{
    TOutput Render(TScene scene, ViewportRenderRequest request);
}
