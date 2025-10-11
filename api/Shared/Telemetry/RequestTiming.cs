using System.Diagnostics;

namespace api.Shared.Telemetry;

public static class RequestTiming
{
    public static Stopwatch Start() => Stopwatch.StartNew();

    public static long Stop(Stopwatch stopwatch)
    {
        if (stopwatch is null) throw new ArgumentNullException(nameof(stopwatch));
        if (stopwatch.IsRunning)
        {
            stopwatch.Stop();
        }

        return stopwatch.ElapsedMilliseconds;
    }
}
