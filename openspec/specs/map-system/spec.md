# Map System

## Purpose

The map system provides spatial awareness for the game world using a dual-layer architecture: a coordinate grid for terrain data, and an auto-generated navigation graph for efficient pathfinding and LLM-friendly spatial reasoning. NPC movement is implemented as a continuous action (`MoveToAction`) without requiring a dedicated map component.

## Requirements

### Requirement: Grid-Based Terrain Map

The system SHALL represent the game world as a 2D grid of cells, each with a terrain type defining walkability and movement speed modifier.

#### Scenario: Load grid map from JSON

- **GIVEN** a valid `MapGridConfig` JSON file
- **WHEN** `MapSystem.Load(config)` is called
- **THEN** the grid is parsed with correct dimensions
- **AND** each cell is assigned a `TerrainDef` with `Walkable` and `SpeedMod`
- **AND** unknown terrain characters default to walkable grassland

#### Scenario: Terrain query

- **GIVEN** a loaded grid map
- **WHEN** `MapSystem.GetTerrain(x, y)` is called
- **THEN** the correct `TerrainDef` is returned for valid coordinates
- **AND** a boundary terrain (walkable=false) is returned for out-of-bounds coordinates

### Requirement: Navigation Graph Generation

The system SHALL automatically generate a navigation graph from POI nodes placed on the grid, using A* pathfinding to compute edge weights between every pair of reachable POIs.

#### Scenario: Graph generation from POIs

- **GIVEN** a grid map with N POI nodes
- **WHEN** `MapSystem.Load(config)` completes
- **THEN** a bidirectional navigation graph is created
- **AND** edges exist between all pairs of mutually reachable POIs
- **AND** edge weights reflect terrain-weighted path costs

#### Scenario: Unreachable nodes

- **GIVEN** a POI on an island surrounded by water (unwalkable)
- **WHEN** the navigation graph is generated
- **THEN** no edges connect the island POI to mainland POIs

### Requirement: A* Grid Pathfinding

The system SHALL provide A* pathfinding on the grid with 8-directional movement, terrain-aware costs, and corner-cutting prevention.

#### Scenario: Find path on open terrain

- **GIVEN** start and goal on walkable grassland
- **WHEN** `FindGridPath(sx, sy, gx, gy)` is called
- **THEN** the shortest path respecting terrain costs is returned

#### Scenario: Path blocked by terrain

- **GIVEN** start and goal separated by unwalkable mountains
- **WHEN** `FindGridPath()` is called
- **THEN** null is returned (no path)

#### Scenario: Terrain cost affects path choice

- **GIVEN** a road (speed_mod=1.5) alongside grassland (speed_mod=1.0)
- **WHEN** computing path between two points
- **THEN** the path prefers road cells when they reduce total cost

### Requirement: Graph Pathfinding for NPCs

The system SHALL provide Dijkstra shortest-path on the navigation graph for NPC movement planning.

#### Scenario: Find route between locations

- **GIVEN** two connected POI nodes
- **WHEN** `FindPathOnGraph("城门", "酒馆")` is called
- **THEN** a `PathResult` is returned with Reachable=true
- **AND** `NodePath` contains the sequence of nodes to traverse
- **AND** `TotalTicks` is the sum of edge `TimeCostTicks`

#### Scenario: Same location

- **GIVEN** fromNodeId equals toNodeId
- **WHEN** `FindPathOnGraph("酒馆", "酒馆")` is called
- **THEN** `Reachable` is true and `NodePath` contains only the single node

### Requirement: Entity Position Index

The system SHALL maintain a bidirectional index mapping entity IDs to their current graph node locations.

#### Scenario: Register entity position

- **GIVEN** a loaded map
- **WHEN** `RegisterEntity("guard", "城门")` is called
- **THEN** `GetEntityLocation("guard")` returns "城门"
- **AND** `GetEntitiesAt("城门")` includes "guard"

#### Scenario: Move entity between locations

- **GIVEN** guard registered at "城门"
- **WHEN** `RegisterEntity("guard", "酒馆")` is called
- **THEN** `GetEntitiesAt("城门")` no longer includes "guard"
- **AND** `GetEntitiesAt("酒馆")` includes "guard"

### Requirement: Spatial Queries

The system SHALL support querying nearby entities and reachable locations based on travel time.

#### Scenario: Nearby entities within range

- **GIVEN** entities at various locations
- **WHEN** `GetNearbyEntities("集市", maxTicks=5)` is called
- **THEN** all entities within 5 ticks travel distance are returned with their tick distance

#### Scenario: Reachable nodes

- **GIVEN** a connected navigation graph
- **WHEN** `GetReachableNodes("集市")` is called
- **THEN** all directly adjacent nodes are returned
