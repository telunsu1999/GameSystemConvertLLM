# LLM Pipeline

## Purpose

The LLM pipeline enables NPC decision-making through a chain of: trigger detection → data collection → semantic transformation → prompt assembly → LLM inference → action resolution. A watchdog-based process manager ensures the Python LLM server stays alive with auto-restart on failure.

## Requirements

### Requirement: Data Collection for LLM Context

The system SHALL collect relevant NPC state data and event history, then transform it into natural language via semantic rules for LLM prompt injection.

#### Scenario: Collect attributes by tags

- **GIVEN** a `CollectConfig` with `attr_tags: ["vital", "world"]`
- **WHEN** `DataCollector.CollectRaw(npcId, config)` is called
- **THEN** only attributes tagged "vital" or "world" are included in raw data

#### Scenario: Collect recent events

- **GIVEN** a `CollectConfig` with `event_tags: ["combat"]`, `event_days: 7`, `event_limit: 5`
- **WHEN** data is collected
- **THEN** up to 5 combat events from the last 7 game-days are included as `memory_events`

#### Scenario: Semantic transformation

- **GIVEN** raw data `{hp: 15, hp_max: 150, location: "城门"}`
- **WHEN** `SemanticEngine.EvaluateAll(rules, data)` is called with `npc_state` rules
- **THEN** output includes `health: "生命值不足10%"` and `location: "当前位置: 城门"`

### Requirement: Semantic Rule Engine

The system SHALL provide a pure-function semantic engine that transforms raw data into human-readable text using JSON-defined rules, with no game-domain knowledge baked in.

#### Scenario: Ratio status rule

- **GIVEN** a rule `{type:"ratio_status", current:"hp", max:"hp_max", thresholds:[...]}`
- **WHEN** `current/max` falls into threshold range `[0, 0.1]`
- **THEN** output is "生命值不足10%"

#### Scenario: Inventory list rule

- **GIVEN** a rule `{type:"inventory_list", field:"inventory", format:"{name}x{count}"}`
- **WHEN** inventory is `{sword:1, potion:3}`
- **THEN** output is "背包: swordx1, potionx3"

### Requirement: Prompt Template Assembly

The system SHALL assemble LLM prompts from a system message (NPC identity + world rules + available tools) and a user message (semantic state + action options).

#### Scenario: Assemble full prompt

- **GIVEN** an NPC with personality, semantic context, and available actions
- **WHEN** `PromptTemplate.Render()` is called
- **THEN** output contains `[system]` and `[user]` sections
- **AND** the user section includes semantic texts and formatted action list

### Requirement: LLM Client Communication

The system SHALL communicate with the Python LLM server via OpenAI-compatible HTTP API on the configured port.

#### Scenario: Health check

- **GIVEN** the LLM server is running on port 8000
- **WHEN** `HealthCheckAsync(cancellationToken)` is called
- **THEN** a GET to `/health` returns 200 OK → true
- **AND** connection failure or non-200 → false within the cancellation timeout

#### Scenario: Structured inference with tools

- **GIVEN** system prompt, user prompt, and tool definitions
- **WHEN** `SendWithToolsAsync(system, user, tools)` is called
- **THEN** a POST to `/v1/chat/completions` is made with OpenAI-compatible JSON
- **AND** the LLM response text is returned

### Requirement: LLM Process Watchdog

The system SHALL manage the Python LLM server as a child process with health monitoring, automatic restart on crash, and configurable retry limits.

#### Scenario: Auto-start on demand

- **GIVEN** no LLM server is running
- **WHEN** `StartAsync(pythonExe, serverDir)` is called
- **THEN** orphaned processes on the port are cleaned up
- **AND** a new Python process is spawned
- **AND** health checks begin with progressive backoff (1s→1.5s→2s→...→5s)
- **AND** status transitions to Online when health check succeeds

#### Scenario: Auto-restart on crash

- **GIVEN** an Online LLM server whose process crashes
- **WHEN** the watchdog detects `HasExited`
- **THEN** a restart is attempted (up to MaxRestarts=3 times)
- **AND** status returns to Online if restart succeeds
- **AND** status becomes Error if all restarts fail

#### Scenario: Startup timeout

- **GIVEN** the LLM server fails to become healthy within StartupTimeout (180s)
- **WHEN** the timeout elapses
- **THEN** the process is killed
- **AND** a restart is attempted (up to max retries)

#### Scenario: Graceful stop

- **GIVEN** a running LLM server
- **WHEN** `Stop()` is called
- **THEN** the watchdog is cancelled
- **AND** the child process is killed
- **AND** status becomes Offline

### Requirement: Offline Model Loading

The Python LLM server SHALL load models from local cache only, without requiring network access to HuggingFace Hub.

#### Scenario: Load from local cache

- **GIVEN** model files cached in `.hf_cache`
- **WHEN** `AutoTokenizer.from_pretrained()` and `AutoModelForCausalLM.from_pretrained()` are called
- **THEN** `local_files_only=True` is passed
- **AND** the model loads without attempting network access
