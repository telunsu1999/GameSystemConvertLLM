## 1. 项目初始化

- [x] 1.1 创建 `src/` 目录和 `requirements.txt`
- [x] 1.2 创建 `models/` 目录，添加 `.gitignore` 忽略模型文件
- [x] 1.3 安装依赖: `transformers`, `torch`, `bitsandbytes`, `fastapi`, `uvicorn`, `accelerate`

## 2. 模型加载模块

- [x] 2.1 实现 `src/model.py`：自动检测GPU/CUDA可用性
- [x] 2.2 实现4-bit量化加载 QWEN3-0.6B（使用 `BitsAndBytesConfig`）
- [x] 2.3 实现文本生成函数 `generate(prompt, max_tokens, temperature)`
- [x] 2.4 添加模型下载脚本 `scripts/download_model.py`

## 3. API服务

- [x] 3.1 实现 `src/schemas.py`：Pydantic请求/响应模型定义
- [x] 3.2 实现 `src/main.py`：FastAPI应用骨架，模型启动加载
- [x] 3.3 实现 `POST /api/v1/generate` 生成端点
- [x] 3.4 实现 `GET /health` 健康检查端点
- [x] 3.5 实现 `GET /api/v1/model/info` 模型信息端点
- [x] 3.6 添加启动脚本 `scripts/start_server.bat`

## 4. 验证

- [x] 4.1 编写 `tests/test_api.py`：测试所有端点基本功能 (17/17 passed)
- [x] 4.2 手动验证：发送中文prompt，检查生成结果合理性 (CPU 6.4s/80tokens)
- [x] 4.3 验证健康检查端点返回正确状态 (status: ok, model_loaded: true)
