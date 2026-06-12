"""
Tests for llm_server package.
Run from project root: pytest llm_server/tests/test_api.py -v
"""

import pytest
from unittest.mock import patch, MagicMock

from llm_server.schemas import (
    GenerateRequest,
    GenerateResponse,
    HealthResponse,
    ModelInfoResponse,
    ErrorResponse,
)


class TestGenerateRequestSchema:
    """Validate GenerateRequest Pydantic model."""

    def test_valid_request(self):
        req = GenerateRequest(prompt="Hello")
        assert req.prompt == "Hello"
        assert req.max_tokens == 256
        assert req.temperature == 0.7
        assert req.top_p == 0.9

    def test_empty_prompt_rejected(self):
        with pytest.raises(Exception):
            GenerateRequest(prompt="")

    def test_max_tokens_too_high_rejected(self):
        with pytest.raises(Exception):
            GenerateRequest(prompt="test", max_tokens=9999)

    def test_temperature_too_high_rejected(self):
        with pytest.raises(Exception):
            GenerateRequest(prompt="test", temperature=3.0)

    def test_temperature_negative_rejected(self):
        with pytest.raises(Exception):
            GenerateRequest(prompt="test", temperature=-1.0)

    def test_top_p_too_high_rejected(self):
        with pytest.raises(Exception):
            GenerateRequest(prompt="test", top_p=1.5)

    def test_custom_params_accepted(self):
        req = GenerateRequest(prompt="Test", max_tokens=100, temperature=0.5)
        assert req.max_tokens == 100
        assert req.temperature == 0.5


class TestResponseSchemas:
    """Validate response Pydantic models."""

    def test_generate_response(self):
        resp = GenerateResponse(text="Hello world", tokens_used=5)
        data = resp.model_dump()
        assert data["text"] == "Hello world"
        assert data["tokens_used"] == 5

    def test_health_response_ok(self):
        resp = HealthResponse(
            status="ok", model_loaded=True, model_name="Qwen/Qwen3-0.6B"
        )
        data = resp.model_dump()
        assert data["status"] == "ok"
        assert data["model_loaded"] is True

    def test_health_response_loading(self):
        resp = HealthResponse(status="loading", model_loaded=False, model_name="")
        data = resp.model_dump()
        assert data["status"] == "loading"
        assert data["model_loaded"] is False

    def test_model_info_response(self):
        resp = ModelInfoResponse(
            name="Qwen/Qwen3-0.6B", device="cuda", quantized=True, loaded=True
        )
        data = resp.model_dump()
        for field in ["name", "device", "quantized", "loaded"]:
            assert field in data

    def test_error_response(self):
        resp = ErrorResponse(detail="Something went wrong")
        assert resp.detail == "Something went wrong"


class TestModelGenerate:
    """Test ModelManager with mocked model."""

    @pytest.fixture
    def mock_model_manager(self):
        with patch("llm_server.model.AutoModelForCausalLM"), \
             patch("llm_server.model.AutoTokenizer"), \
             patch("llm_server.model.torch.cuda.is_available", return_value=False):
            from llm_server.model import ModelManager
            mgr = ModelManager()
            mgr.model = MagicMock()
            mgr.tokenizer = MagicMock()
            mgr.device = "cpu"
            return mgr

    def test_generate_raises_when_not_loaded(self):
        from llm_server.model import ModelManager
        mgr = ModelManager()
        with pytest.raises(RuntimeError, match="not loaded"):
            mgr.generate("test")

    def test_is_loaded_false_initially(self):
        from llm_server.model import ModelManager
        mgr = ModelManager()
        assert mgr.is_loaded() is False

    def test_get_info_returns_metadata(self):
        from llm_server.model import ModelManager
        mgr = ModelManager()
        info = mgr.get_info()
        assert info["name"] == "Qwen/Qwen3-0.6B"
        assert info["loaded"] is False

    @patch("llm_server.model.torch.cuda.get_device_properties")
    @patch("llm_server.model.torch.cuda.get_device_name", return_value="Test GPU")
    @patch("llm_server.model.torch.cuda.is_available", return_value=True)
    def test_detect_device_gpu(self, mock_avail, mock_name, mock_props):
        mock_props.return_value = MagicMock(total_mem=4 * 1024**3)
        from llm_server.model import ModelManager
        mgr = ModelManager()
        device = mgr.detect_device()
        assert device == "cuda"

    @patch("llm_server.model.torch.cuda.is_available", return_value=False)
    def test_detect_device_cpu(self, mock_avail):
        from llm_server.model import ModelManager
        mgr = ModelManager()
        device = mgr.detect_device()
        assert device == "cpu"
