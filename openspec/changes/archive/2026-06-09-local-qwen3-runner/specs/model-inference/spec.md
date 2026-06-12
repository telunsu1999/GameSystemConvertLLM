## ADDED Requirements

### Requirement: Load QWEN3-0.6B model
系统 SHALL 支持加载QWEN3-0.6B模型用于文本生成推理。系统 MUST 支持4-bit量化以减少内存占用。系统 SHALL 自动检测GPU可用性并优先使用GPU。

#### Scenario: GPU可用时使用GPU加载
- **WHEN** 系统启动且检测到CUDA GPU可用
- **THEN** 模型 MUST 加载到GPU显存中

#### Scenario: GPU不可用时回退CPU
- **WHEN** 系统启动且未检测到GPU
- **THEN** 模型 MUST 使用CPU推理（通过llama-cpp或CPU模式）

#### Scenario: 4-bit量化模型加载
- **WHEN** 系统配置使用4-bit量化
- **THEN** 模型加载后显存/内存占用 SHALL 不超过600MB

### Requirement: Text generation
系统 SHALL 接收prompt文本，返回模型生成的续写文本。系统 MUST 支持配置最大生成token数、温度等参数。

#### Scenario: 基本文本生成
- **WHEN** 发送包含prompt的生成请求
- **THEN** 系统返回模型生成的文本，响应时间 SHALL 在30秒超时内

#### Scenario: 空prompt处理
- **WHEN** 发送空字符串作为prompt
- **THEN** 系统返回错误信息，状态码400
