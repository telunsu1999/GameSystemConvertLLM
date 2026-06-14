"""
Qwen3.5-0.8B Inference Server.

Loads the model directly via HuggingFace Transformers (no vLLM required).
Provides an OpenAI-compatible /v1/chat/completions endpoint.

Configuration is read from configs/server.json (shared with GameLoop C# client).

Usage:
  python -m llm_server.main
  python -m llm_server.main --port 8080
"""

import argparse
import json
import logging
import os
import shutil
import time
from contextlib import asynccontextmanager
from datetime import datetime
from pathlib import Path
from typing import Any, Optional

# ============================================================
# MUST set HF_HOME before any HF imports (model cache location)
# ============================================================

def _find_repo_root() -> Path:
    current = Path(__file__).resolve().parent
    for _ in range(5):
        if (current / "configs" / "server.json").exists():
            return current
        current = current.parent
    return Path(__file__).resolve().parent.parent

def _load_server_config() -> dict:
    config_path = _find_repo_root() / "configs" / "server.json"
    if config_path.exists():
        with open(config_path, "r", encoding="utf-8") as f:
            return json.load(f)
    print(f"[WARN] Config not found: {config_path}, using defaults")
    return {}

_config = _load_server_config()
SERVER_HOST = _config.get("host", "127.0.0.1")
SERVER_PORT = _config.get("vllm_port", 8000)
MODEL_NAME = _config.get("model_name", "Qwen/Qwen3.5-0.8B")

# Resolve model cache dir (relative to repo root)
_repo_root = _find_repo_root()
_cache_dir = _config.get("model_cache_dir", ".hf_cache")
_cache_path = Path(_cache_dir)
if not _cache_path.is_absolute():
    _cache_path = _repo_root / _cache_path
_cache_path = str(_cache_path.resolve())

if "HF_HOME" not in os.environ:
    os.environ["HF_HOME"] = _cache_path
    print(f"[CACHE] Model cache: {_cache_path}")

# --- Now safe to import HF libraries ---
import torch
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field
from transformers import AutoModelForCausalLM, AutoTokenizer

# ============================================================
# Traffic logging (with auto-backup on startup)
# ============================================================

_LOG_DIR = Path(__file__).parent.parent / "logs"
_LOG_DIR.mkdir(exist_ok=True)
_LOG_FILE = _LOG_DIR / "llm_server_traffic.log"
_MAX_BACKUPS = 5

def _rotate_log():
    """Backup old log if exists, keep last N backups."""
    if _LOG_FILE.exists():
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup = _LOG_DIR / f"llm_server_traffic_{timestamp}.log"
        shutil.move(str(_LOG_FILE), str(backup))
        print(f"[LOG] Backed up old log → {backup.name}")

        # Prune old backups (keep last _MAX_BACKUPS)
        backups = sorted(
            _LOG_DIR.glob("llm_server_traffic_*.log"),
            key=lambda p: p.stat().st_mtime,
            reverse=True,
        )
        for old in backups[_MAX_BACKUPS:]:
            old.unlink()
            print(f"[LOG] Pruned old backup: {old.name}")

_rotate_log()

_traffic_logger = logging.getLogger("llm_traffic")
_traffic_logger.setLevel(logging.DEBUG)
_traffic_handler = logging.FileHandler(
    str(_LOG_FILE), encoding="utf-8"
)
_traffic_handler.setFormatter(
    logging.Formatter("%(asctime)s | %(message)s", datefmt="%H:%M:%S")
)
_traffic_logger.addHandler(_traffic_handler)
_traffic_logger.propagate = False

# ============================================================
# Model manager
# ============================================================

class ModelManager:
    def __init__(self):
        self.model = None
        self.tokenizer = None
        self.device = None
        self._loaded = False

    def is_loaded(self) -> bool:
        return self._loaded

    def load(self):
        print(f"[INFO] Loading {MODEL_NAME} ...")
        self.device = "cuda" if torch.cuda.is_available() else "cpu"

        if self.device == "cuda":
            gpu = torch.cuda.get_device_name(0)
            vram = torch.cuda.get_device_properties(0).total_memory / 1024**3
            print(f"[INFO] GPU: {gpu} ({vram:.1f} GB)")

        self.tokenizer = AutoTokenizer.from_pretrained(
            MODEL_NAME, trust_remote_code=True, local_files_only=True
        )
        self.model = AutoModelForCausalLM.from_pretrained(
            MODEL_NAME,
            trust_remote_code=True,
            local_files_only=True,
            torch_dtype=torch.float16 if self.device == "cuda" else torch.float32,
            device_map="auto" if self.device == "cuda" else "cpu",
        )
        self.model.eval()
        self._loaded = True
        print(f"[INFO] Model loaded on {self.device.upper()}")

    def generate(
        self,
        messages: list[dict],
        max_tokens: int = 256,
        temperature: float = 0.7,
        top_p: float = 0.9,
        enable_thinking: bool = False,
        tools: list[dict] | None = None,
        tool_choice: str | None = None,
    ) -> dict:
        template_kwargs = {
            "tokenize": False,
            "add_generation_prompt": True,
            "enable_thinking": enable_thinking,
        }
        if tools:
            template_kwargs["tools"] = tools
            template_kwargs["tool_choice"] = tool_choice or "auto"

        text = self.tokenizer.apply_chat_template(
            messages, **template_kwargs
        )
        inputs = self.tokenizer(text, return_tensors="pt").to(self.model.device)

        with torch.no_grad():
            outputs = self.model.generate(
                **inputs,
                max_new_tokens=max_tokens,
                do_sample=temperature > 0,
                temperature=temperature if temperature > 0 else 1.0,
                top_p=top_p,
                pad_token_id=self.tokenizer.eos_token_id,
            )

        generated_ids = outputs[0][len(inputs.input_ids[0]):]
        response_text = self.tokenizer.decode(generated_ids, skip_special_tokens=True)

        return {"text": response_text, "tokens_used": len(generated_ids)}


_model = ModelManager()

# ============================================================
# FastAPI app
# ============================================================

@asynccontextmanager
async def lifespan(app: FastAPI):
    print("=" * 50)
    print(f"  {MODEL_NAME} Inference Server")
    print("=" * 50)
    _traffic_logger.info(
        "========== SERVER START %s ==========",
        datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    )
    _model.load()
    yield

app = FastAPI(
    title=f"{MODEL_NAME} Inference API",
    description="OpenAI-compatible inference server for Qwen3.5-0.8B",
    version="1.0.0",
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


# ============================================================
# OpenAI-compatible schemas
# ============================================================

class ChatMessage(BaseModel):
    role: str
    content: str

class ChatCompletionRequest(BaseModel):
    model: str = MODEL_NAME
    messages: list[ChatMessage]
    max_tokens: int = Field(default=256, ge=1, le=8192)
    temperature: float = Field(default=0.7, ge=0.0, le=2.0)
    top_p: float = Field(default=0.9, ge=0.0, le=1.0)
    enable_thinking: bool = False
    tools: Optional[list[dict[str, Any]]] = None
    tool_choice: Any = None  # "auto" | "none" | "required" | {"type":"function","function":{"name":"xxx"}}


# ============================================================
# Endpoints
# ============================================================

@app.get("/health")
async def health_check():
    return {
        "status": "ok" if _model.is_loaded() else "loading",
        "model": MODEL_NAME,
        "device": _model.device or "unknown",
    }


@app.post("/v1/chat/completions")
async def chat_completions(req: ChatCompletionRequest):
    if not _model.is_loaded():
        raise HTTPException(status_code=503, detail="Model is still loading")

    req_id = datetime.now().strftime("%H%M%S%f")[:12]
    messages_dict = [m.model_dump() for m in req.messages]

    _traffic_logger.info(
        "========== REQUEST %s ==========\n"
        "max_tokens=%s  temperature=%s  top_p=%s  enable_thinking=%s\n"
        "--- MESSAGES ---\n%s\n--- END MESSAGES ---",
        req_id, req.max_tokens, req.temperature, req.top_p,
        req.enable_thinking, messages_dict,
    )

    t0 = time.perf_counter()
    try:
        result = _model.generate(
            messages=messages_dict,
            max_tokens=req.max_tokens,
            temperature=req.temperature,
            top_p=req.top_p,
            enable_thinking=req.enable_thinking,
            tools=req.tools,
            tool_choice=req.tool_choice,
        )
        elapsed = time.perf_counter() - t0

        _traffic_logger.info(
            "========== RESPONSE %s ==========\n"
            "tokens=%s  elapsed=%.2fs\n%s\n========== END ==========",
            req_id, result["tokens_used"], elapsed, result["text"],
        )

        return {
            "id": f"chatcmpl-{req_id}",
            "object": "chat.completion",
            "created": int(time.time()),
            "model": MODEL_NAME,
            "choices": [{
                "index": 0,
                "message": {"role": "assistant", "content": result["text"]},
                "finish_reason": "stop",
            }],
            "usage": {
                "prompt_tokens": 0,
                "completion_tokens": result["tokens_used"],
                "total_tokens": result["tokens_used"],
            },
        }
    except Exception as e:
        elapsed = time.perf_counter() - t0
        _traffic_logger.error(
            "REQUEST %s | ERROR after %.2fs: %s", req_id, elapsed, str(e)
        )
        raise HTTPException(status_code=500, detail=str(e))


# ============================================================
# Entry point
# ============================================================

def main():
    parser = argparse.ArgumentParser(description=f"{MODEL_NAME} Inference Server")
    parser.add_argument("--host", default=SERVER_HOST)
    parser.add_argument("--port", type=int, default=SERVER_PORT)
    args = parser.parse_args()

    import uvicorn
    uvicorn.run(
        "llm_server.main:app",
        host=args.host,
        port=args.port,
        log_level="info",
    )


if __name__ == "__main__":
    main()
