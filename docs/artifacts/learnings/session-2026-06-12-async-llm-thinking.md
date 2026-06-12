# Session Summary - 2026-06-12

## Async LLM Planning with Real-time Thinking State

### What Was Done

#### 1. Attributes.OnChanged Event System
- Added `public event Action<string, object> OnChanged` to `Attributes` class
- Event fires in `Set()` after `AddToIndex()`, carrying (key, newValue)
- Enables real-time attribute change notification for UI

#### 2. LlmPlanner Async Conversion
- Replaced blocking `.Result` call with `async/await` fire-and-forget pattern
- Added `_busy` flag to prevent concurrent LLM calls
- Sets `llm_thinking = 1` before LLM call, resets to `0` in `finally` block
- Captures tick context at start to avoid race conditions
- Added `_onSession` callback for reporting LLM results

#### 3. GameLoop Event Forwarding
- `OnNpcAttrChanged` event: forwards `(entityId, key, value)` to subscribers
- `OnPlanSession` event: forwards `LlmSession` result to subscribers
- `AddEntity()` auto-subscribes to each entity's `Attributes.OnChanged`
- `LoadEntities()` wires `OnPlanSession` callback into `LlmPlanner`

#### 4. WebSocket Real-time Updates (Program.cs)
- `attr_changed` messages sent on attribute change
- `llm_done` messages sent on LLM plan completion (prompt, response, latency, steps)
- Both forwarded to frontend via WebSocket

#### 5. Frontend Thinking State (index.html)
- NPC cards show yellow glow border + spinning indicator when `llm_thinking = 1`
- Thinking badge auto-appears/removes via `attr_changed` WS messages
- LLM tab shows session records with prompt/response/latency via `llm_done` messages

#### 6. Daily 8 AM Plan Trigger
- Added `"每日晨间规划"` trigger to all 3 NPCs (blacksmith, guard, merchant)
- Condition: `hour == 8`, mode: `llm`, action: `decide`
- Triggers async LLM planning every in-game day at 8:00 AM

#### 7. LLM Health Check Fix (LLMManager.cs)
- **Bug**: `attempts` counter never reset, accumulated across health checks
- **Fix**: Reset `attempts = 0` on successful health check
- Increased failure threshold from 3 to 15 (30s instead of 6s)
- Prevents LLM from incorrectly flipping to "Stopped" during busy periods

#### 8. LLM Session Recording
- New classes: `LlmSession`, `LlmPlanResult` in `LlmPlanner.cs`
- `LlmPlanner` invokes `_onSession` callback on plan success (with prompt, response, latency, steps) and on error
- Frontend `renderSessions()` displays sessions in LLM tab

### Files Changed (13 files)

| File | Change |
|------|--------|
| `src/Entity/Attributes/Attributes.cs` | +OnChanged event, fires in Set() |
| `src/Entity/LlmPlanner/LlmPlanner.cs` | Async conversion, _onSession callback, LlmSession classes |
| `src/GameLoop/GameLoop.cs` | OnNpcAttrChanged + OnPlanSession events, wiring |
| `src/GameLoop/GameLoop.csproj` | +LlmPlanner + LLMClient references |
| `src/System/LLM/Manager/LLMManager.cs` | Health check attempts reset fix |
| `demo/Playground/Program.cs` | OnPlanSession subscription, llm_done WS message |
| `demo/Playground/wwwroot/index.html` | Thinking state CSS/JS, attr_changed handler |
| `configs/entities/blacksmith.json` | +llm_thinking attribute |
| `configs/entities/guard.json` | +llm_thinking attribute |
| `configs/entities/merchant.json` | +llm_thinking attribute |
| `configs/triggers/blacksmith.json` | +每日晨间规划 trigger (H8) |
| `configs/triggers/guard.json` | +每日晨间规划 trigger (H8) |
| `configs/triggers/merchant.json` | +每日晨间规划 trigger (H8) |

### Architecture

```
LlmPlanner.OnTick (fire-and-forget)
  |
  +-> attrs.Set("llm_thinking", 1)  -->  Attributes.OnChanged
  |                                        |
  |                                        +-> GameLoop.OnNpcAttrChanged
  |                                              |
  |                                              +-> WebSocket "attr_changed"
  |                                                    |
  |                                                    +-> Frontend NPC card thinking badge
  |
  +-> await LLM response
  |
  +-> parse JSON plan, schedule steps
  |
  +-> _onSession(LlmSession)  -->  GameLoop.OnPlanSession
  |                                  |
  |                                  +-> WebSocket "llm_done" (prompt, response, latency)
  |                                        |
  |                                        +-> Frontend LLM tab session record
  |
  +-> finally: attrs.Set("llm_thinking", 0)  -->  thinking badge removed
```
