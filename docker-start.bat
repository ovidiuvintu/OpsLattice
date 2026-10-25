@echo off
setlocal

cd /d "%~dp0"

rem Usage:
rem   docker-start.bat            Clean rebuild (no build cache, fresh base images)
rem   docker-start.bat --fast     Rebuild using the Docker build cache (quicker)
rem   docker-start.bat --prune    Clean rebuild and also clear Docker's whole build cache,
rem                               including the saved NuGet and npm downloads

set MODE=clean
if /I "%~1"=="--fast" set MODE=fast
if /I "%~1"=="/fast" set MODE=fast
if /I "%~1"=="--prune" set MODE=prune
if /I "%~1"=="/prune" set MODE=prune
rem Older flags still work and mean a clean rebuild.
if /I "%~1"=="--no-cache" set MODE=clean
if /I "%~1"=="/nocache" set MODE=clean

set BUILD_ARGS=--no-cache --pull
if /I "%MODE%"=="fast" set BUILD_ARGS=

echo ========================================
echo       OpsLattice Docker Startup
echo       Mode: %MODE%
echo ========================================
echo.

echo [1/6] Checking Docker...
docker info >nul 2>&1
if errorlevel 1 (
    echo ERROR: Docker is not running.
    echo Please start Docker Desktop.
    pause
    exit /b 1
)

echo.
echo [2/6] Stopping existing containers...
docker compose down --remove-orphans
if errorlevel 1 goto error

echo.
if /I "%MODE%"=="prune" (
    echo [3/6] Clearing Docker build cache...
    docker builder prune -f
    if errorlevel 1 goto error
) else (
    echo [3/6] Keeping Docker build cache.
)

echo.
echo [4/6] Building Docker images...
docker compose build %BUILD_ARGS%
if errorlevel 1 goto error

echo.
echo [5/6] Starting fresh containers...
docker compose up -d --force-recreate
if errorlevel 1 goto error

rem Remove the untagged images left behind by the rebuild.
docker image prune -f >nul 2>&1

echo.
echo [6/6] Checking container status...
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
