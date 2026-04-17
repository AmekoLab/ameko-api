@echo off
REM FPTU Capstone AMKCollective - Docker Helper Script (Windows)
REM Usage: docker-helper.bat [command]

setlocal enabledelayedexpansion

set "ENV_FILE=.env"

if "%1"=="" goto :help

if "%1"=="setup" goto :setup
if "%1"=="pull" goto :pull
if "%1"=="start" goto :start
if "%1"=="stop" goto :stop
if "%1"=="restart" goto :restart
if "%1"=="logs" goto :logs
if "%1"=="build" goto :build
if "%1"=="clean" goto :clean
if "%1"=="status" goto :status
if "%1"=="health" goto :health
if "%1"=="update" goto :update
if "%1"=="help" goto :help

echo Unknown command: %1
goto :help

:setup
echo [INFO] Setting up environment...
if not exist .env (
    copy .env.example .env
    echo [SUCCESS] .env file created from .env.example
    echo [INFO] Please edit .env with your actual values
) else (
    echo [INFO] .env already exists
)

docker --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not installed!
    exit /b 1
)

docker compose version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker Compose is not installed!
    exit /b 1
)

echo [SUCCESS] Docker setup complete!
goto :eof

:pull
call :check_env
echo [INFO] Pulling latest image from Docker Hub...
docker compose pull
echo [SUCCESS] Image pulled successfully!
goto :eof

:start
call :check_env
echo [INFO] Starting containers...
docker compose up -d
echo [SUCCESS] Containers started!
echo [INFO] API running at http://localhost:8080
echo [INFO] Nginx Proxy Manager admin at http://localhost:81
echo [INFO] Run 'docker-helper.bat logs' to view logs
goto :eof

:stop
echo [INFO] Stopping containers...
docker compose down
echo [SUCCESS] Containers stopped!
goto :eof

:restart
echo [INFO] Restarting containers...
docker compose restart
echo [SUCCESS] Containers restarted!
goto :eof

:logs
docker compose logs -f --tail=100 api
goto :eof

:build
echo [INFO] Building Docker image locally...
docker compose build --no-cache
echo [SUCCESS] Build complete!
goto :eof

:clean
echo [INFO] Cleaning up Docker resources...
docker compose down -v --rmi local
echo [SUCCESS] Cleanup complete!
goto :eof

:status
echo [INFO] Container status:
docker compose ps
echo.
echo [INFO] Resource usage:
docker stats --no-stream
goto :eof

:health
echo [INFO] Checking health status...
curl -f http://localhost:8080/health
if errorlevel 1 (
    echo [ERROR] API health check failed!
    exit /b 1
) else (
    echo [SUCCESS] API is healthy!
)
goto :eof

:update
echo [INFO] Updating to latest version...
call :pull
call :stop
call :start
echo [SUCCESS] Update complete!
goto :eof

:help
echo FPTU Capstone AMKCollective - Docker Helper (Windows)
echo.
echo Usage: docker-helper.bat [command]
echo.
echo Commands:
echo   setup       Setup environment (.env file)
echo   pull        Pull latest image from Docker Hub
echo   start       Start containers
echo   stop        Stop containers
echo   restart     Restart containers
echo   logs        View container logs (follow mode)
echo   build       Build Docker image locally
echo   clean       Clean up containers, volumes, and images
echo   status      Show container status and resource usage
echo   health      Check API health
echo   update      Pull latest image and restart
echo   help        Show this help message
echo.
echo Examples:
echo   docker-helper.bat setup
echo   docker-helper.bat start
echo   docker-helper.bat logs
goto :eof

:check_env
if not exist %ENV_FILE% (
    echo [ERROR] .env file not found!
    echo [INFO] Creating from .env.example...
    copy .env.example .env
    echo [INFO] Please edit .env file with your configuration
    exit /b 1
)
goto :eof
