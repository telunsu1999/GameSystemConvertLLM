@echo off
echo ==================================================
echo   QWEN3-0.6B Inference Server
echo ==================================================
echo.
echo Starting server on http://127.0.0.1:8000
echo Test console: http://127.0.0.1:8000
echo API docs:     http://127.0.0.1:8000/docs
echo.

cd /d "%~dp0.."
.venv\Scripts\python.exe -m llm_server.main %*

pause
