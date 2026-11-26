# agora-argus

Two independent systems that talk only through RabbitMQ:

- **Agora**: a community forum with its own moderation (AI-assisted report checks)
- **Argus**: a health-monitoring and analytics platform that any app can plug into (Agora is its first client)

Advanced Programming project, 2026–2027.

## Branches

- `main`: releases only
- `dev`: development (default branch)
- `feature/AA-<id>-<name>` / `fix/AA-<id>-<name>`: one per task, merged into `dev` through a pull request

Setup and run instructions will be added here.

## Agora database foundation (AA-15)

Agora uses its own SQL Server database (`AgoraDb`) and ASP.NET Core Identity; it does not share Argus data. The database contains Identity users and roles plus forums, posts, flat comments, likes, reports, and community rules. `ApplicationUser` adds `DisplayName`, `Bio`, `IsPro`, and `IsBanned`.

Set the local connection string and development seed passwords in the **Agora.Web** User Secrets store (never in source control):

```powershell
dotnet user-secrets set "ConnectionStrings:AgoraDb" "Server=tcp:127.0.0.1,1433;Database=AgoraDb;User Id=sa;Password=<your-local-sa-password>;TrustServerCertificate=True" --project src/Agora.Web
dotnet user-secrets set "DevelopmentSeed:AdminPassword" "<development-password>" --project src/Agora.Web
dotnet user-secrets set "DevelopmentSeed:ModeratorPassword" "<development-password>" --project src/Agora.Web
dotnet user-secrets set "DevelopmentSeed:MemberPassword" "<development-password>" --project src/Agora.Web
```

Development startup applies migrations and seeds data. The idempotent seed creates roles `Admin`, `Moderator`, and `Member`; accounts `agora.admin` (`admin@agora.local`), `agora.moderator` (`moderator@agora.local`), `agora.member1` (`member1@agora.local`), and `agora.member2` (`member2@agora.local`); and four community rules. It never runs outside Development.

Migration commands:

```powershell
dotnet ef database update --project src/Agora.Web --startup-project src/Agora.Web
dotnet ef migrations add <Name> --project src/Agora.Web --startup-project src/Agora.Web
```

Run non-integration tests with:

```powershell
dotnet test --filter "Category!=Integration"
```

## Agora monitoring integration (AA-20)

Agora uses the shared `Argus.Client` SDK to announce startup, emit heartbeats every 10 seconds, and collect HTTP request metrics. Heartbeats include a real SQL Server connectivity check for `AgoraDb`; RabbitMQ outages are handled by the SDK's reconnecting connection and non-throwing sender, so normal Agora requests continue to work while messages are temporarily unavailable.

Set the broker password in **Agora.Web** User Secrets; the tracked configuration deliberately contains no password. The local broker username defaults to `argus`, matching `.env.example`—use the username configured in your local untracked `.env` if it differs.

```powershell
dotnet user-secrets set "Argus:RabbitMq:Password" "<your-rabbitmq-password>" --project src/Agora.Web
```

For integration verification, start `rabbitmq` and `sqlserver` with Docker, then run Argus.Web and Agora.Web separately. Confirm the seeded `agora` ClientApps row in ArgusDb receives a recent heartbeat and becomes Healthy; browse Agora to produce metrics. Stop RabbitMQ while Agora remains running and confirm pages still work, then restart RabbitMQ and confirm subsequent heartbeats resume. The Argus homepage is intentionally only a placeholder; use database or message-level verification until its dashboard task is complete.


### Sending events and gauges (AA-24)

```csharp
// Program.cs: a gauge is read on every heartbeat (its service comes from a fresh scope, so a DbContext is fine)
builder.Services.AddArgus(builder.Configuration.GetSection("Argus"))
    .AddGauge("reports.pending", async (AgoraDbContext db) =>
        await db.Reports.CountAsync(report => report.Status == ReportStatus.Pending));

// Anywhere: inject IArgusTracker and send an event (Value defaults to 1)
await argus.TrackAsync("post.liked", dimension: forum.Name, targetId: post.Id.ToString(), targetLabel: post.Title);
await argus.TrackAsync("subscription.purchased", value: 4.99m);
```

`TrackAsync` never throws because of RabbitMQ: while the broker is down the event is logged and dropped. A gauge that throws is logged and left out of that heartbeat. Gauge names starting with `sys.` are reserved.


### Reacting to messages from Argus (AA-26)

```csharp
// Program.cs: each handler gets its service from a fresh scope (a DbContext is fine)
builder.Services.AddArgus(builder.Configuration.GetSection("Argus"))
    .OnMaintenanceModeChanged((MaintenanceState state, MaintenanceModeChanged message) => state.SetAsync(message.Enabled, message.Message))
    .OnHealthStatusChanged(async (AgoraDbContext db, HealthStatusChanged message) => { /* store it for the Argus panel */ });
```

The SDK consumes `<clientId>.inbox`. A handler that throws is retried, and after 3 failures the message goes to `<clientId>.dead`. Delivery is at least once, so a handler should skip a `MessageId` it has already seen. Message types without a handler are acknowledged and ignored.


## Docker Infrastructure (AA-4)

The project uses Docker Compose to run RabbitMQ and SQL Server 2022.

### Prerequisites
- Docker Desktop with Linux containers enabled
- Docker Compose

### Setup

1. Copy the environment template:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Configure `RABBITMQ_USER`, `RABBITMQ_PASSWORD`,
   and `MSSQL_SA_PASSWORD` in `.env`.

3. Start the infrastructure:

   ```powershell
   docker compose up -d rabbitmq sqlserver
   ```

4. Verify that both containers are healthy:

   ```powershell
   docker compose ps
   ```

### Service Connections

| Service | Address |
|---|---|
| RabbitMQ AMQP | localhost:5672 |
| RabbitMQ Management UI | http://localhost:15672 |
| SQL Server | tcp:127.0.0.1,1433 |

RabbitMQ Management uses the credentials from `.env`.

SQL Server uses SQL Authentication with username `sa`
and the password from `MSSQL_SA_PASSWORD`.
For local development, enable Trust Server Certificate.

### Data Persistence

RabbitMQ and SQL Server use named Docker volumes
to preserve their data between container restarts.

### Stopping Services

```powershell
docker compose down
```

This stops the services without deleting their volumes.

**Security:** `.env` is Git-ignored and must never be committed.
Only `.env.example` is included in the repository.
## AI Integration (AA-3)

Agora uses the **Google Gemini API** (`gemini-3.5-flash-lite`) for AI-assisted moderation of reported posts. The AI provides recommendations, while moderators make the final decisions.

### Implementation

- **Service:** `src/Agora.Web/Services/GeminiClient.cs`
- **Tests:** `tests/Agora.Web.Tests/GeminiClientTests.cs`
- **API key:** Stored in ASP.NET Core User Secrets (`Llm:ApiKey`) for local development and supplied through an untracked `.env` file (`LLM_API_KEY`) for Docker. Never committed to GitHub.

### Automated Testing

Run the Gemini tests:

```powershell
dotnet test tests/Agora.Web.Tests/Agora.Web.Tests.csproj
```

To enable the live API test in PowerShell:

```powershell
$env:RUN_GEMINI_LIVE_TEST = "1"
dotnet test tests/Agora.Web.Tests/Agora.Web.Tests.csproj
Remove-Item Env:\RUN_GEMINI_LIVE_TEST
```

The live test is skipped by default to avoid unnecessary API requests.

### Free-Tier Rate Limits

Verified in Google AI Studio on November 21, 2026, for the **Default Gemini Project**:

| Metric | Limit |
|---|---:|
| Requests per minute (RPM) | 15 |
| Input tokens per minute (TPM) | 250,000 |
| Requests per day (RPD) | 500 |

Rate limits are project-specific and may change. Exceeding them can result in HTTP 429 responses.
