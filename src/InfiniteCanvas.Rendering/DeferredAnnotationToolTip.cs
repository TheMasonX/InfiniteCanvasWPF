namespace InfiniteCanvas.Rendering;

public sealed class DeferredAnnotationToolTip(SampleAnnotation annotation)
{
    public override string ToString()
    {
        return AnnotationFeaturePresenter.BuildTooltipContent(annotation);
    }
}