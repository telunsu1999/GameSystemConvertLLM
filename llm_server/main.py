"""
FastAPI inference server for QWEN3-0.6B.

Endpoints:
  GET  /                  - Test console (HTML page)
  GET  /health            - Health check with model status
  GET  /api/v1/model/info - Model metadata
  POST /api/v1/generate   - Text generation

Usage:
  python -m llm_server.main
  python -m llm_server.main --port 8080
"""

import argparse
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

from llm_server.model import model_manager
from llm_server.schemas import (
    GenerateRequest,
    GenerateResponse,
    HealthResponse,
    ModelInfoResponse,
    ErrorResponse,
)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Load model on startup, clean up on shutdown."""
    print("=" * 50)
    print("  QWEN3-0.6B Inference Server")
    print("=" * 50)
    model_manager.load_model()
    yield


app = FastAPI(
    title="QWEN3-0.6B Inference API",
    description="Local inference server for QWEN3-0.6B model",
    version="0.1.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Static test console
_static_dir = Path(__file__).parent / "static"
if _static_dir.exists():
    app.mount("/static", StaticFiles(directory=str(_static_dir)), name="static")

    @app.get("/")
    async def root():
        return FileResponse(str(_static_dir / "index.html"))


@app.get("/health", response_model=HealthResponse)
async def health_check():
    return HealthResponse(
        status="ok" if model_manager.is_loaded() else "loading",
        model_loaded=model_manager.is_loaded(),
        model_name=model_manager.model_name if model_manager.is_loaded() else "",
    )


@app.get("/api/v1/model/info", response_model=ModelInfoResponse)
async def model_info():
    info = model_manager.get_info()
    return ModelInfoResponse(**info)


@app.post("/api/v1/generate", response_model=GenerateResponse)
async def generate(req: GenerateRequest):
    if not model_manager.is_loaded():
        raise HTTPException(status_code=503, detail="Model is still loading")
    try:
        result = model_manager.generate(
            prompt=req.prompt,
            max_tokens=req.max_tokens,
            temperature=req.temperature,
            top_p=req.top_p,
            enable_thinking=req.enable_thinking,
        )
        return GenerateResponse(**result)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


def main():
    parser = argparse.ArgumentParser(description="QWEN3-0.6B Inference Server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    args = parser.parse_args()

    import uvicorn
    uvicorn.run(
        "llm_server.main:app",
        host=args.host,
        port=args.port,
        reload=False,
    )


if __name__ == "__main__":
    main()
