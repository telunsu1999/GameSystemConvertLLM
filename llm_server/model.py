"""
QWEN3-0.6B model loading and text generation module.
Supports GPU (CUDA) with 4-bit quantization and CPU fallback.
"""

import torch
from transformers import (
    AutoModelForCausalLM,
    AutoTokenizer,
    BitsAndBytesConfig,
)


class ModelManager:
    """Manages model lifecycle: loading, generation, resource tracking."""

    def __init__(self):
        self.model = None
        self.tokenizer = None
        self.device = None
        self.is_quantized = False
        self.model_name = "Qwen/Qwen3-4B"

    def detect_device(self) -> str:
        """Auto-detect available device. Returns 'cuda' or 'cpu'."""
        if torch.cuda.is_available():
            self.device = "cuda"
            gpu_name = torch.cuda.get_device_name(0)
            print(f"[INFO] GPU detected: {gpu_name}")
            print(f"[INFO] CUDA version: {torch.version.cuda or 'unknown'}")
            print(f"[INFO] VRAM: {torch.cuda.get_device_properties(0).total_memory / 1024**3:.1f} GB")
        else:
            self.device = "cpu"
            print("[INFO] No GPU detected, using CPU inference")
        return self.device

    def load_model(self, model_name: str = None) -> None:
        """Load QWEN3-0.6B with 4-bit quantization if GPU available."""
        if model_name is None:
            model_name = self.model_name

        self.detect_device()

        if self.device == "cuda":
            self._load_quantized(model_name)
        else:
            self._load_cpu(model_name)

    def _load_quantized(self, model_name: str) -> None:
        """Load model with 4-bit NF4 quantization for GPU."""
        print(f"[INFO] Loading {model_name} with 4-bit quantization...")

        bnb_config = BitsAndBytesConfig(
            load_in_4bit=True,
            bnb_4bit_compute_dtype=torch.float16,
            bnb_4bit_use_double_quant=True,
            bnb_4bit_quant_type="nf4",
        )

        self.model = AutoModelForCausalLM.from_pretrained(
            model_name,
            quantization_config=bnb_config,
            device_map="auto",
            trust_remote_code=True,
            torch_dtype=torch.float16,
        )
        self.tokenizer = AutoTokenizer.from_pretrained(
            model_name,
            trust_remote_code=True,
        )
        self.is_quantized = True
        print(f"[INFO] Model loaded on GPU with 4-bit quantization")

    def _load_cpu(self, model_name: str) -> None:
        """Load model for CPU inference (no quantization)."""
        print(f"[INFO] Loading {model_name} for CPU inference...")

        self.model = AutoModelForCausalLM.from_pretrained(
            model_name,
            device_map="cpu",
            trust_remote_code=True,
            torch_dtype=torch.float32,
        )
        self.tokenizer = AutoTokenizer.from_pretrained(
            model_name,
            trust_remote_code=True,
        )
        self.is_quantized = False
        print(f"[INFO] Model loaded on CPU")

    def generate(
        self,
        prompt: str,
        max_tokens: int = 256,
        temperature: float = 0.7,
        top_p: float = 0.9,
        enable_thinking: bool = False,
        tools: list[dict] = None,
        tool_choice: str = None,
    ) -> dict:
        """Generate text from prompt. Supports function calling via tools/tool_choice."""
        if self.model is None or self.tokenizer is None:
            raise RuntimeError("Model not loaded. Call load_model() first.")

        messages = [
            {"role": "system", "content": "You are a helpful AI assistant."},
            {"role": "user", "content": prompt},
        ]

        # Build chat template kwargs
        template_kwargs = {
            "tokenize": False,
            "add_generation_prompt": True,
            "enable_thinking": enable_thinking,
        }
        if tools:
            template_kwargs["tools"] = tools
            # tool_choice default to "auto" if tools provided but not specified
            template_kwargs["tool_choice"] = tool_choice or "auto"

        text = self.tokenizer.apply_chat_template(
            messages,
            **template_kwargs,
        )

        inputs = self.tokenizer([text], return_tensors="pt")
        if self.device == "cuda":
            inputs = {k: v.to("cuda") for k, v in inputs.items()}

        with torch.no_grad():
            outputs = self.model.generate(
                **inputs,
                max_new_tokens=max_tokens,
                temperature=temperature if temperature > 0 else 1.0,
                top_p=top_p,
                do_sample=temperature > 0,
                pad_token_id=self.tokenizer.eos_token_id,
            )

        input_len = inputs["input_ids"].shape[1]
        generated_ids = outputs[0][input_len:]
        generated_text = self.tokenizer.decode(
            generated_ids,
            skip_special_tokens=True,
        )

        return {
            "text": generated_text.strip(),
            "tokens_used": len(generated_ids),
        }

    def is_loaded(self) -> bool:
        """Check if model is loaded and ready."""
        return self.model is not None and self.tokenizer is not None

    def get_info(self) -> dict:
        """Return model metadata."""
        return {
            "name": self.model_name,
            "device": self.device or "unknown",
            "quantized": self.is_quantized,
            "loaded": self.is_loaded(),
        }


# Global singleton for FastAPI integration
model_manager = ModelManager()
