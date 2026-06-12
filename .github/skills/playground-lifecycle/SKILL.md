# Playground 启停流程

## 启动

```powershell
# 1. 清理残留
Get-Process -Name dotnet,python -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3

# 2. 启动 Playground（会自动启动 LLM）
dotnet run --project demo/Playground/Playground.csproj --no-launch-profile
# mode: async, timeout: 120000ms

# 3. 等待 LLM 就绪（最多 30s）
# 轮询 http://localhost:8000/health 直到返回 {"status":"ok",...}
# 然后在浏览器打开 http://localhost:5000

# 4. 如果 LLM 未启动，前端点 Start 按钮
```

## 停止

```powershell
# 关闭整个进程树（dotnet 的 shutdown hook 会杀 Python）
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
# 清理可能残留的 Python
Get-Process -Name python -ErrorAction SilentlyContinue | Stop-Process -Force
```

## 验证

```powershell
# Playground 是否在监听
netstat -an | Select-String ":5000.*LISTEN"

# LLM 是否响应
Invoke-WebRequest -Uri http://localhost:8000/health -UseBasicParsing
```

## 规则

- 仅用 `dotnet run` 启动，不手动启动 Python LLM 服务
- Playground 退出时必须杀残留 Python 进程
- 每次重启前先 kill dotnet + python
- 不在 `run_in_terminal` 中手动 curl 检查 LLM，SS用浏览器 UI 的 LLM 状态指示
