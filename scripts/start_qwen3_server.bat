@echo off
setlocal enabledelayedexpansion

REM -- Read config --
for /f "tokens=*" %%i in ('powershell -NoProfile -Command "(Get-Content '%~dp0..\configs\server.json' | ConvertFrom-Json).vllm_port"') do set SERVER_PORT=%%i
if "%SERVER_PORT%"=="" set SERVER_PORT=8000

for /f "tokens=*" %%i in ('powershell -NoProfile -Command "(Get-Content '%~dp0..\configs\server.json' | ConvertFrom-Json).model_cache_dir"') do set CACHE_DIR=%%i
if "%CACHE_DIR%"=="" set CACHE_DIR=.hf_cache

REM -- Set model cache to project dir (not C: drive) --
set HF_HOME=%~dp0..\%CACHE_DIR%
if not exist "%HF_HOME%" mkdir "%HF_HOME%"

echo ==================================================
echo   Qwen3.5-0.8B Inference Server
echo ==================================================
echo.
echo Starting server on http://127.0.0.1:%SERVER_PORT%
echo API:         http://127.0.0.1:%SERVER_PORT%/v1/chat/completions
echo Health:      http://127.0.0.1:%SERVER_PORT%/health
echo.
echo NOTE: Model loads on first startup (GPU required).
echo.

cd /d "%~dp0.."
.venv\Scripts\python.exe -m llm_server.main --port %SERVER_PORT% %*

pause
