## Context

本项目需要一个可本地运行的QWEN3-0.6B推理环境，为后续NPC行为驱动实验提供基础。目标平台为Windows PC，用户本地有NVIDIA GPU（具体型号待确认）。模型量化后约400MB，CPU也可运行。

## Goals / Non-Goals

**Goals:**
- 搭建最小可用的本地推理服务，暴露HTTP API
- 支持GPU（CUDA）和CPU两种推理后端
- 使用4-bit量化降低资源占用
- 纯Python实现，依赖最少

**Non-Goals:**
- 不涉及游戏逻辑集成
- 不涉及微调或训练
- 不做批量推理优化
- 不做流式输出（SSE）

## Decisions

### Decision 1: 推理后端选择 transformers + bitsandbytes

**选择**: 使用 `transformers` 库 + `bitsandbytes` 4-bit量化加载

**备选**:
- `llama-cpp-python`: CPU推理优秀但GPU支持较弱，且QWEN3的GGUF格式支持可能不完善
- `vLLM`: 功能强大但过于重量级，不适合0.6B小模型

**理由**: transformers是HuggingFace官方库，对QWEN3支持最完善；bitsandbytes提供成熟的4-bit量化。后续如需CPU优先可切换到llama-cpp。

### Decision 2: Web框架选择 FastAPI

**选择**: FastAPI + uvicorn

**备选**: Flask, aiohttp

**理由**: FastAPI自带异步支持、自动生成OpenAPI文档、请求验证（Pydantic），适合后续扩展。依赖轻量。

### Decision 3: 单进程单模型架构

**选择**: 服务启动时加载模型一次，所有请求复用同一模型实例

**备选**: 每次请求加载/卸载模型、多worker多模型

**理由**: 0.6B模型小，加载快但也没必要反复加载。单实例避免显存碎片。0.6B模型单次推理足够快，无需多worker。

### Decision 4: 项目结构

```
src/
  main.py          # FastAPI入口，启动服务
  model.py         # 模型加载和管理
  schemas.py       # Pydantic请求/响应模型
models/            # 模型文件存放（.gitignore）
requirements.txt   # Python依赖
```

## Risks / Trade-offs

- [风险] 模型首次下载时间长（约1.2GB） → 提供下载脚本，显示进度条
- [风险] bitsandbytes在Windows上安装可能有问题 → 提供llama-cpp作为备选方案
- [风险] 4-bit量化可能影响生成质量 → 先验证，后续可切换到8-bit或FP16
- [取舍] 不支持流式输出 → 0.6B生成短文本，流式收益不大；后续有需要再加
