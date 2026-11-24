namespace Argus.Client;

/// <summary>
/// Request counters filled by the UseArgusMetrics middleware and emptied by every heartbeat,
/// so each heartbeat reports only the requests since the previous one.
/// </summary>
public sealed class RequestMetrics
{
    private readonly Lock _lock = new(); // requests run in parallel
    private int _requests;
    private int _errors;
    private double _totalMs;

    public void Record(double elapsedMs, bool failed)
    {
        lock (_lock)
        {
            _requests++;
            if (failed) _errors++;
            _totalMs += elapsedMs;
        }
    }

    public (int Requests, int Errors, double AvgResponseMs) TakeAndReset()
    {
        lock (_lock)
        {
            var snapshot = (_requests, _errors, _requests == 0 ? 0 : _totalMs / _requests);
            _requests = 0;
            _errors = 0;
            _totalMs = 0;
            return snapshot;
        }
    }
}