"""Pydantic request and response schemas for the inference API."""

from typing import Any, Optional
from pydantic import BaseModel, Field


class GenerateRequest(BaseModel):
    """Request body for /api/v1/generate endpoint."""
    prompt: str = Field(..., min_length=1, description="Input prompt text")
    max_tokens: int = Field(
        default=256,
        ge=1,
        le=2048,
        description="Maximum number of tokens to generate",
    )
    temperature: float = Field(
        default=0.7,
        ge=0.0,
        le=2.0,
        description="Sampling temperature (0 = greedy)",
    )
    top_p: float = Field(
        default=0.9,
        ge=0.0,
        le=1.0,
        description="Nucleus sampling threshold",
    )
    enable_thinking: bool = Field(
        default=False,
        description="Enable QWEN3 thinking/reasoning tags",
    )
    tools: Optional[list[dict[str, Any]]] = Field(
        default=None,
        description="OpenAI-format tool definitions for function calling",
    )
    tool_choice: Optional[str] = Field(
        default=None,
        description="Tool choice: 'auto', 'required', 'none', or specific tool name",
    )


class GenerateResponse(BaseModel):
    """Response body for /api/v1/generate endpoint."""
    text: str = Field(..., description="Generated text")
    tokens_used: int = Field(..., description="Number of tokens generated")


class HealthResponse(BaseModel):
    """Response body for /health endpoint."""
    status: str = Field(..., description="Service status: 'ok' or 'loading'")
    model_loaded: bool = Field(..., description="Whether model is loaded")
    model_name: str = Field(default="", description="Model identifier")


class ModelInfoResponse(BaseModel):
    """Response body for /api/v1/model/info endpoint."""
    name: str = Field(..., description="Model name")
    device: str = Field(..., description="Device: 'cuda' or 'cpu'")
    quantized: bool = Field(..., description="Whether using quantization")
    loaded: bool = Field(..., description="Whether model is loaded and ready")


class ErrorResponse(BaseModel):
    """Standard error response."""
    detail: str = Field(..., description="Error description")
