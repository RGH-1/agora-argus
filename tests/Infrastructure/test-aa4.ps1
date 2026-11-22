
$ErrorActionPreference = "Stop"

Write-Host "Running AA-4 infrastructure tests..."

# 1. Validate Docker Compose
docker compose config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "FAIL: Docker Compose configuration"
}
Write-Host "PASS: Docker Compose configuration"

# 2. Verify environment files
if (!(Test-Path ".env.example")) {
    throw "FAIL: .env.example missing"
}

git check-ignore -q .env
if ($LASTEXITCODE -ne 0) {
    throw "FAIL: .env is not Git-ignored"
}
Write-Host "PASS: Environment configuration"

# 3. Verify container health
foreach ($service in @("rabbitmq", "sqlserver")) {
    $id = docker compose ps -q $service

    if (!$id) {
        throw "FAIL: $service container missing"
    }

    $status = docker inspect `
        --format '{{.State.Health.Status}}' $id

    if ($status -ne "healthy") {
        throw "FAIL: $service status is $status"
    }

    Write-Host "PASS: $service is healthy"
}

# 4. Check RabbitMQ application
docker compose exec -T rabbitmq `
    rabbitmq-diagnostics -q check_running

if ($LASTEXITCODE -ne 0) {
    throw "FAIL: RabbitMQ diagnostics"
}
Write-Host "PASS: RabbitMQ diagnostics"

# 5. Verify host ports
foreach ($port in @(5672, 15672, 1433)) {
    $connected = Test-NetConnection 127.0.0.1 `
        -Port $port -InformationLevel Quiet `
        -WarningAction SilentlyContinue

    if (!$connected) {
        throw "FAIL: Port $port"
    }

    Write-Host "PASS: Port $port"
}

Write-Host "AA-4: ALL CHECKS PASSED" -ForegroundColor Green
