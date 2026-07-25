namespace InfiniteCanvas.Core;

public sealed class RenderRequestTracker
{
    private int _currentVersion;

    public int CurrentVersion => Volatile.Read(ref _currentVersion);

    public int BeginRequest()
    {
        return Interlocked.Increment(ref _currentVersion);
    }

    public void Advance()
    {
        Interlocked.Increment(ref _currentVersion);
    }

    public bool IsCurrent(int version)
    {
        return Volatile.Read(ref _currentVersion) == version;
    }
}
