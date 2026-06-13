"""
Download Qwen3.5-0.8B model from HuggingFace with progress display.
Supports resume (auto-detects already-downloaded files).

Usage:
  python scripts/download_qwen3.py
  python scripts/download_qwen3.py --force  (re-download all files)
"""

import os
import json
import sys
import time
from pathlib import Path

os.environ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1"

# --- Set cache dir from configs/server.json ---
def _find_repo_root() -> Path:
    current = Path(__file__).resolve().parent
    for _ in range(5):
        if (current / "configs" / "server.json").exists():
            return current
        current = current.parent
    return Path(__file__).resolve().parent.parent

_repo_root = _find_repo_root()
_config_path = _repo_root / "configs" / "server.json"
if _config_path.exists():
    with open(_config_path, "r", encoding="utf-8") as f:
        _config = json.load(f)
    _cache_dir = _config.get("model_cache_dir", ".hf_cache")
else:
    _cache_dir = ".hf_cache"

_cache_path = Path(_cache_dir)
if not _cache_path.is_absolute():
    _cache_path = _repo_root / _cache_path
_cache_path = str(_cache_path.resolve())

if "HF_HOME" not in os.environ:
    os.environ["HF_HOME"] = _cache_path
print(f"[CACHE] Model will be saved to: {_cache_path}")

from huggingface_hub import snapshot_download, hf_hub_download
from huggingface_hub.utils import tqdm as hf_tqdm
from huggingface_hub import get_hf_file_metadata


MODEL_NAME = "Qwen/Qwen3.5-4B"

MODEL_FILES = [
    "model.safetensors",
    "config.json",
    "tokenizer.json",
    "tokenizer_config.json",
    "vocab.json",
    "merges.txt",
    "generation_config.json",
]


def format_size(size_bytes: int) -> str:
    """Format bytes to human-readable string."""
    if size_bytes >= 1024**3:
        return f"{size_bytes / 1024**3:.2f} GB"
    elif size_bytes >= 1024**2:
        return f"{size_bytes / 1024**2:.1f} MB"
    elif size_bytes >= 1024:
        return f"{size_bytes / 1024:.0f} KB"
    return f"{size_bytes} B"


def get_file_metadata(filename: str):
    """Get remote file metadata (size, etc.). Returns None if unreachable."""
    try:
        return get_hf_file_metadata(
            f"https://huggingface.co/{MODEL_NAME}/resolve/main/{filename}"
        )
    except Exception:
        return None


def is_file_cached(filename: str) -> bool:
    """Check if a file is already fully downloaded in HF cache."""
    try:
        hf_hub_download(MODEL_NAME, filename, local_files_only=True)
        return True
    except Exception:
        return False


def main():
    force = "--force" in sys.argv

    print("=" * 55)
    print(f"  Qwen3.5-4B Model Download")
    print(f"  Model: {MODEL_NAME}")
    print("=" * 55)
    print()

    # Step 1: Check what needs downloading
    print("[Scan] Checking remote file list...")
    total_size = 0
    to_download = []
    already_cached = []

    for f in MODEL_FILES:
        meta = get_file_metadata(f)
        if meta is None:
            print(f"  [WARN] Cannot reach HF Hub for: {f}")
            continue

        file_size = meta.size
        cached = is_file_cached(f) and not force

        if cached:
            already_cached.append((f, file_size))
        else:
            to_download.append((f, file_size))
            total_size += file_size

    print(f"  Already cached: {len(already_cached)} files")
    print(f"  To download:    {len(to_download)} files ({format_size(total_size)})")

    if already_cached:
        cached_total = sum(s for _, s in already_cached)
        print(f"  Cached size:    {format_size(cached_total)}")

    if not to_download:
        print()
        print("[DONE] All files are already cached!")
        return

    print()

    # Step 2: Download
    print(f"[Download] Fetching {len(to_download)} file(s)...")
    print("          (large files may take minutes without HF_TOKEN)")
    print()

    start_time = time.time()

    try:
        local_path = snapshot_download(
            MODEL_NAME,
            resume_download=True,
            tqdm_class=hf_tqdm,
        )
    except Exception as e:
        print(f"\n[ERROR] Download failed: {e}")
        print("Tips:")
        print("  1. Check your internet connection")
        print("  2. Set HF_TOKEN for faster downloads:")
        print("     https://huggingface.co/settings/tokens")
        print("  3. Re-run to resume partial download")
        sys.exit(1)

    elapsed = time.time() - start_time

    print()
    print("=" * 55)
    print(f"  Download Complete!")
    print(f"  Time:  {elapsed:.0f}s")
    print(f"  Cache: {local_path}")
    print("=" * 55)
    print()
    print("Next: python src/main.py")
    print("Docs: http://127.0.0.1:8000/docs")


if __name__ == "__main__":
    main()
