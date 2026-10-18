
@echo off
setlocal

cd /d "%~dp0"

echo ========================================
echo       OpsLattice Docker Startup
echo ========================================
echo.

echo [1/4] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running.
    echo Please start Docker Desktop.
    pause
    exit /b 1
)

echo.
echo [2/4] Building Docker images...
docker compose build
if errorlevel 1 goto error

echo.
echo [3/4] Starting containers...
docker compose up -d
if errorlevel 1 goto error

echo.
echo [4/4] Checking container status...
docker compose ps
if errorlevel 1 goto error

echo.
echo ========================================
echo       OpsLattice Started
echo ========================================
echo.
echo UI:  http://localhost:8080
echo API: http://localhost:5160
echo.
echo View logs: docker compose logs -f
echo Stop:      docker compose down
echo.

start "" "http://localhost:8080"

exit /b 0

:error
echo.
echo ERROR: Docker build or startup failed.
pause
exit /b 1
