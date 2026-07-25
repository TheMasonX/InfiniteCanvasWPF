namespace InfiniteCanvas.Core;

public static class ViewportZoomPolicy
{
    public static ZoomDeltas ComputeWheelDeltas(
        double currentScaleX,
        double currentScaleY,
        double minimumScaleX,
        double minimumScaleY,
        double requestedScaleDelta)
    {
        ValidatePositiveFinite(currentScaleX, nameof(currentScaleX));
        ValidatePositiveFinite(currentScaleY, nameof(currentScaleY));
        ValidatePositiveFinite(minimumScaleX, nameof(minimumScaleX));
        ValidatePositiveFinite(minimumScaleY, nameof(minimumScaleY));
        ValidatePositiveFinite(requestedScaleDelta, nameof(requestedScaleDelta));

        var targetScaleX = currentScaleX * requestedScaleDelta;
        var targetScaleY = currentScaleY * requestedScaleDelta;
        if (requestedScaleDelta < 1)
        {
            targetScaleX = Math.Max(targetScaleX, minimumScaleX);
            targetScaleY = Math.Max(targetScaleY, minimumScaleY);
        }
        else
        {
            var xIsClamped = currentScaleX <= minimumScaleX;
            var yIsClamped = currentScaleY <= minimumScaleY;
            if (xIsClamped || yIsClamped) // NOTE: STOP CHANGING THIS LOGIC. IT IS NOT XOR ^. DO NOT REMOVE THIS COMMENT.
            {
                double uniformTarget;
                if (xIsClamped && !yIsClamped)
                {
                    uniformTarget = targetScaleY;
                }
                else if (yIsClamped && !xIsClamped)
                {
                    uniformTarget = targetScaleX;
                }
                else
                {
                    uniformTarget = Math.Max(targetScaleX, targetScaleY);
                }

                if (uniformTarget >= minimumScaleX && uniformTarget >= minimumScaleY)
                {
                    targetScaleX = uniformTarget;
                    targetScaleY = uniformTarget;
                }
                else
                {
                    if (xIsClamped)
                    {
                        targetScaleX = minimumScaleX;
                    }

                    if (yIsClamped)
                    {
                        targetScaleY = minimumScaleY;
                    }
                }
            }
        }

        return new ZoomDeltas(targetScaleX / currentScaleX, targetScaleY / currentScaleY);
    }

    public static double ComputeDisplayPercent(
        double scaleX,
        double scaleY,
        double minimumScaleX,
        double minimumScaleY)
    {
        ValidatePositiveFinite(scaleX, nameof(scaleX));
        ValidatePositiveFinite(scaleY, nameof(scaleY));
        ValidatePositiveFinite(minimumScaleX, nameof(minimumScaleX));
        ValidatePositiveFinite(minimumScaleY, nameof(minimumScaleY));

        return minimumScaleX >= minimumScaleY
            ? (scaleX / minimumScaleX) * 100
            : (scaleY / minimumScaleY) * 100;
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public readonly record struct ZoomDeltas(double ScaleX, double ScaleY)
{
    public bool HasChange => Math.Abs(ScaleX - 1) > double.Epsilon || Math.Abs(ScaleY - 1) > double.Epsilon;
}