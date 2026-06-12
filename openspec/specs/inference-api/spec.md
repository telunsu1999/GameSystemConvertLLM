### Requirement: HTTP生成接口
系统 MUST 提供 `/api/v1/generate` 端点，接受POST请求，包含prompt和可选参数，返回生成的文本。

#### Scenario: 成功生成
- **WHEN** 发送 `{"prompt": "你好，请介绍一下自己"}` 到 `/api/v1/generate`
- **THEN** 返回200状态码，响应体包含 `{"text": "...", "tokens_used": N}`

#### Scenario: 参数控制
- **WHEN** 发送 `{"prompt": "...", "max_tokens": 100, "temperature": 0.7}` 到生成端点
- **THEN** 生成结果 MUST 不超过100个token，并使用temperature=0.7采样

### Requirement: 健康检查接口
系统 MUST 提供 `/health` 端点，返回服务运行状态和模型加载状态。

#### Scenario: 模型已加载
- **WHEN** 模型成功加载后访问 `/health`
- **THEN** 返回200状态码，`{"status": "ok", "model_loaded": true, "model_name": "Qwen/Qwen3-0.6B"}`

#### Scenario: 模型未加载
- **WHEN** 模型尚未加载完成时访问 `/health`
- **THEN** 返回503状态码，`{"status": "loading", "model_loaded": false}`

### Requirement: 模型信息接口
系统 MUST 提供 `/api/v1/model/info` 端点，返回当前加载模型的元信息。

#### Scenario: 查询模型信息
- **WHEN** 访问 `/api/v1/model/info`
- **THEN** 返回 `{"name": "Qwen/Qwen3-0.6B", "backend": "transformers", "device": "cuda", "quantization": "4bit"}`
