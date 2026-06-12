# Flow Workbench

一个独立的手工测试工具，用浏览器把当前仓库里的完整流程串起来：

- 填写 NPC 基础信息和属性
- 添加事件与 perception
- 调用 C# 的 AttributeModule / EventSystem / DataCollector / PromptTemplate
- 生成 prompt
- 可选把 prompt 继续发送给 `local_qwen3_runner`

## 启动

在仓库根目录执行：

```powershell
dotnet run --project manual_flow_tool/FlowWorkbench/FlowWorkbench.csproj
```

默认地址：

```text
http://localhost:5000
```

如果要联调 LLM，再单独启动：

```powershell
.venv\Scripts\python.exe -m local_qwen3_runner.main --port 8000
```

## 页面能力

- `只生成提示词`：只走 C# 流程
- `生成并调用 LLM`：C# 流程完成后，POST 到 `/api/v1/generate`
- `检查健康状态`：检查 Python runner 的 `/health`

## 说明

- 角色的 `name`、`personality`、`prompt_template` 会由页面顶部字段自动注入
- 其余属性由页面中的属性列表注入
- 事件时间当前固定为录入时刻，因为现有 `EventSystem.Record()` 不支持外部指定时间