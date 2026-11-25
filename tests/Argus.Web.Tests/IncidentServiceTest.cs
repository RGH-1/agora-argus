using Argus.Contracts;
using Argus.Web.Data;
using Argus.Web.Data.Entities;
using Argus.Web.Repositories;
using Argus.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.Web.Tests;

public class IncidentServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ArgusDbContext _db = null!;
    private IncidentService _service = null!;
    private Incident _incident = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new ArgusDbContext(new DbContextOptionsBuilder<ArgusDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync(); // seeds agora and demo
        _service = new IncidentService(new IncidentRepository(_db));

        _incident = new Incident { ClientAppId = 1, Cause = HealthStatus.Down, Reason = "No heartbeat", OpenedAtUtc = DateTime.UtcNow };
        _db.Incidents.Add(_incident);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Operator_acknowledges_adds_notes_and_resolves()
    {
        Assert.True(await _service.AcknowledgeAsync(_incident.Id));
        Assert.Equal(IncidentStatus.Acknowledged, _incident.Status);
        Assert.Same(_incident, await _service.GetOpenAsync(1)); // acknowledged still counts as open

        Assert.True(await _service.SaveNotesAsync(_incident.Id, "  Restarted the container  "));
        Assert.Equal("Restarted the container", _incident.Notes);

        Assert.True(await _service.ResolveAsync(_incident.Id));
        Assert.Equal(IncidentStatus.Resolved, _incident.Status);
        Assert.NotNull(_incident.ResolvedAtUtc);
        Assert.Null(await _service.GetOpenAsync(1));

        Assert.True(await _service.AcknowledgeAsync(_incident.Id)); // too late: stays Resolved
        Assert.Equal(IncidentStatus.Resolved, _incident.Status);
    }

    [Fact]
    public async Task Missing_incident_or_too_long_notes_are_rejected()
    {
        Assert.False(await _service.ResolveAsync(999));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveNotesAsync(_incident.Id, new string('x', 2001)));
    }
}