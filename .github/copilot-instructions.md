# GameSystemConvertLLM

## 规则

- 修改代码前先列诊断结论和方案，等确认后再执行
- 所有游戏逻辑 JSON 配置驱动
- 组件化：GameEntity 容器 + IEntityComponent 插件

## OpenSpec 规范（新会话主动读取）

| 规范 | 内容 |
|------|------|
| `openspec/specs/entity-system/spec.md` | 实体组件模型、Attributes、JSON配置 |
| `openspec/specs/action-system/spec.md` | IAction/IContinuousAction、MoveToAction |
| `openspec/specs/map-system/spec.md` | Grid+Graph地图、MapSystem、A*/Dijkstra |
| `openspec/specs/llm-pipeline/spec.md` | LLM链路、SemanticEngine、LLMManager保活 |
| `openspec/specs/game-loop/spec.md` | WorldClock、Tick驱动、StateSnapshot |
| `openspec/specs/ui-layout/spec.md` | 三栏布局、Canvas地图、WebSocket |
| `openspec/specs/config-system/spec.md` | JSON配置（实体/动作/语义/地图/调度） |

## 关键入口

- `demo/Playground/Program.cs` — 入口
- `demo/Playground/wwwroot/index.html` — 前端
- `llm_server/main.py` — LLM服务
- `src/GameLoop/GameLoop.cs` — 主循环
- `configs/server.json` — 共享配置

## 常用命令

```powershell
dotnet run --project demo/Playground/Playground.csproj --urls "http://127.0.0.1:5555"
dotnet build demo/Playground/Playground.csproj
Get-Process python,dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```
