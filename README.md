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
