# Configuration System

## Purpose

The configuration system enables all game behavior to be defined through JSON files rather than hardcoded logic. Entity definitions, action mappings, schedules, triggers, semantic rules, and map layouts are all JSON-configurable. C# and Python share a common server configuration.

## Requirements

### Requirement: JSON Entity Definition

The system SHALL define NPC entities entirely through JSON files with external file references for subsystems.

#### Scenario: Complete entity config

- **GIVEN** `configs/entities/guard.json`
- **WHEN** the config is loaded
- **THEN** the entity has:
  - `id` and `name` for identification
  - `attrs[]` array with typed key-value pairs and tag categorization
  - `schedule` path pointing to a schedule config
  - `triggers` path pointing to trigger rules
  - `actions` array pointing to action mapping files
  - `goals` path (optional)
  - `relations` path (optional)

#### Scenario: Location consistency

- **GIVEN** entity configs and map config
- **WHEN** loaded together
- **THEN** every entity's `location` attribute value MUST match a `poi_nodes[].id` in the map config

### Requirement: Action JSON Mapping

The system SHALL support defining actions in JSON with typed steps, conditions, interruption handlers, and parameter schemas.

#### Scenario: Action step types

- **GIVEN** an action definition in JSON
- **WHEN** the action executes
- **THEN** supported step types include:
  - `set_attr` — modify an attribute value
  - `event` — emit a game event with tags and data
  - `check_path` — verify path existence via MapSystem
  - `schedule_path_legs` — chain movement sub-actions

#### Scenario: Action parameter schema

- **GIVEN** an action with `params` defined
- **WHEN** the LLM needs to know available actions
- **THEN** `GetAvailableActions()` returns the parameter schemas for tool definition
- **AND** `options_from` enables dynamic option lists (e.g., "reachable_locations")

### Requirement: Schedule Configuration

The system SHALL support time-based scheduling with hour-level and tick-level granularity, plus recurring intervals.

#### Scenario: Schedule at specific hour

- **GIVEN** schedule item `{action_type:"move_to", at_hour:8, params:{target:"哨所"}}`
- **WHEN** game hour reaches 8
- **THEN** the `move_to` action is triggered with target "哨所"

#### Scenario: Recurring action

- **GIVEN** schedule item `{action_type:"eat", every_hour:6}`
- **WHEN** every 6 game hours elapse
- **THEN** the `eat` action is triggered

### Requirement: Trigger Rule Configuration

The system SHALL support condition-based triggers that produce either direct actions or LLM decision requests.

#### Scenario: Direct mode trigger

- **GIVEN** trigger rule `{mode:"direct", conditions:[...], action_type:"move_to"}`
- **WHEN** all conditions evaluate true
- **THEN** the action is executed immediately via ActionResolver

#### Scenario: LLM mode trigger

- **GIVEN** trigger rule `{mode:"llm", conditions:[...], description:"..."}`
- **WHEN** all conditions evaluate true
- **THEN** the rule is forwarded to LlmPlanner for LLM decision

### Requirement: Semantic Rule Configuration

The system SHALL support multiple rule types for transforming raw game data into natural language text.

#### Scenario: Supported rule types

- **GIVEN** semantic rule files in `configs/semantic/`
- **WHEN** rules are loaded
- **THEN** supported types include: `ratio_status`, `key_value`, `inventory_list`, `event_summary`, `comparison`, `conditional`

### Requirement: Map Configuration

The system SHALL define maps using a compact grid string array with terrain character codes and POI node placements.

#### Scenario: Grid encoding

- **GIVEN** a map config with `grid: ["MMMM", "GRRG"]`
- **WHEN** loaded
- **THEN** width=4, height=2
- **AND** cell (0,0) is Mountain, (1,0) is Grassland, (2,0) is Road

#### Scenario: POI node placement

- **GIVEN** `poi_nodes: [{id:"城门", grid_x:5, grid_y:2, type:"plaza"}]`
- **WHEN** the map is loaded
- **THEN** the node is registered at grid position (5,2)
- **AND** it becomes a node in the generated navigation graph

### Requirement: Shared Server Configuration

The system SHALL use a single `configs/server.json` read by both the C# Playground and the Python LLM server.

#### Scenario: C# reads config

- **GIVEN** `configs/server.json` with `model_name`, `vllm_port`, `model_cache_dir`
- **WHEN** `ServerConfig.Load(repoRoot)` is called in C#
- **THEN** the configuration is available for LLMClient construction

#### Scenario: Python reads config

- **GIVEN** the same `configs/server.json`
- **WHEN** `llm_server/main.py` starts
- **THEN** `_load_server_config()` reads `model_name` and uses it for model loading
