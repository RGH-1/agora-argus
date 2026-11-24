namespace Argus.Client.Tests;

public class RequestMetricsTests
{
    [Fact]
    public void Counts_requests_and_errors_then_starts_over()
    {
        var metrics = new RequestMetrics();
        metrics.Record(10, failed: false);
        metrics.Record(30, failed: true);

        Assert.Equal((2, 1, 20.0), metrics.TakeAndReset());
        Assert.Equal((0, 0, 0.0), metrics.TakeAndReset()); // the next heartbeat only reports new requests
    }
}