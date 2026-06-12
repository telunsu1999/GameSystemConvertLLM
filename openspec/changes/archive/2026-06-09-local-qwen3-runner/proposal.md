## Why

在模拟经营游戏中使用LLM驱动NPC行为之前，首先需要搭建一个可本地运行的推理环境作为实验基础。选择QWEN3-0.6B是因为它是目前最小的可用中文模型之一，适合评估“极小模型驱动NPC”的可行性边界。本次变更聚焦于搭建环境、暴露API，不涉及游戏逻辑。

## What Changes

- 搭建本地Python推理环境，加载QWEN3-0.6B模型（通过transformers或llama-cpp）
- 提供HTTP API接口，接收prompt并返回模型生成的文本
- 支持GPU和CPU两种推理后端
- 提供简单的健康检查和模型信息查询端点

## Capabilities

### New Capabilities
- `model-inference`: 加载QWEN3-0.6B模型并提供文本生成推理能力
- `inference-api`: 提供HTTP API，包括生成接口、健康检查、模型信息查询

### Modified Capabilities
<!-- 无已有capability需要修改 -->

## Impact

- 依赖: transformers, torch, fastapi, uvicorn (或 llama-cpp-python)
- 需要下载QWEN3-0.6B模型文件（约1.2GB，4-bit量化后约400MB）
- 新增目录: `src/` (推理服务代码), `models/` (模型存放)
