# Web UI Layout

## Purpose

The web frontend provides real-time visualization of the game world through a three-panel layout: NPC list (left), Canvas map (center), and NPC detail (right). A collapsible bottom panel hosts event logs, LLM sessions, trace output, and debug tools. All updates flow through a single WebSocket connection.

## Requirements

### Requirement: Three-Panel Layout

The system SHALL present the game UI in three panels: a left NPC list (200px), a center Canvas map (flexible), and a right detail panel (320px).

#### Scenario: Initial layout

- **GIVEN** the page loads and WebSocket connects
- **WHEN** `map_data` and `state` messages are received
- **THEN** the NPC list renders all entities with HP bar, location, and status
- **AND** the map renders terrain grid with POI nodes and NPC positions
- **AND** the detail panel shows placeholder text

#### Scenario: Select NPC from list

- **GIVEN** the NPC list is rendered
- **WHEN** a list item is clicked
- **THEN** that NPC is highlighted in the list (red border)
- **AND** the NPC is highlighted on the map (red ring)
- **AND** the detail panel shows full NPC info (HP/SP bars, goals, attributes, schedule)

#### Scenario: Select NPC from map

- **GIVEN** the map is rendered with NPC circles
- **WHEN** an NPC circle is clicked (within hit radius)
- **THEN** the same selection behavior occurs as list click
- **AND** the NPC list highlight updates to match

### Requirement: Canvas Map Rendering

The system SHALL render the game map on an HTML5 Canvas with terrain coloring, POI markers, NPC indicators, and support for zoom and pan.

#### Scenario: Terrain rendering

- **GIVEN** map data with grid and terrain definitions
- **WHEN** `render()` is called
- **THEN** each visible grid cell is filled with its terrain color
- **AND** grid lines are drawn when zoom >= 1.0
- **AND** off-screen cells are culled (not drawn)

#### Scenario: POI node rendering

- **GIVEN** map data with POI nodes
- **WHEN** `render()` is called at zoom >= 0.8
- **THEN** each POI is drawn as a colored circle
- **AND** a name label is shown above the circle
- **AND** colors differ by node type (commercial=yellow, plaza=green, military=red, etc.)

#### Scenario: NPC rendering

- **GIVEN** NPC states with location attributes
- **WHEN** `render()` is called
- **THEN** each NPC is drawn at its location node's grid position
- **AND** stationary NPCs are green circles, moving NPCs are blue with arc indicator
- **AND** dead NPCs are gray
- **AND** selected NPC has a red outer ring
- **AND** thinking NPC has a yellow outer ring
- **AND** name labels are shown when zoom >= 0.7

#### Scenario: Mouse wheel zoom

- **GIVEN** the map is rendered
- **WHEN** the mouse wheel is scrolled
- **THEN** zoom changes by factor 1.15 (in) or 0.87 (out)
- **AND** zoom is clamped to [0.3, 5.0]
- **AND** the zoom center is the cursor position

#### Scenario: Mouse drag pan

- **GIVEN** the map is rendered
- **WHEN** the mouse is pressed, moved, and released
- **THEN** the view offset tracks the drag distance
- **AND** the map is re-rendered during drag

### Requirement: Real-Time State Updates

The system SHALL update the UI in real-time as game state changes arrive via WebSocket.

#### Scenario: Attribute change updates

- **GIVEN** an `attr_changed` WebSocket message for `location`
- **WHEN** the message is received
- **THEN** `npcStates[id].location` is updated
- **AND** the map re-renders (NPC moves to new position)
- **AND** the NPC list re-renders (location text updates)
- **AND** if the NPC is selected, the detail panel refreshes

#### Scenario: HP change updates

- **GIVEN** an `attr_changed` WebSocket message for `hp`
- **WHEN** the message is received
- **THEN** the NPC list HP bar updates
- **AND** if the NPC is selected, the detail panel HP bar updates

### Requirement: LLM Status Display

The system SHALL display the LLM server status in the top bar with distinct visual states.

#### Scenario: Status states

- **GIVEN** an `llm_status` WebSocket message
- **WHEN** `online=true, running=true` → green "LLM: Online" with pulse animation
- **WHEN** `online=false, running=true` → yellow "LLM: Starting..." with spin animation
- **WHEN** `error=true` → red "LLM: Error" with red dot
- **WHEN** `running=false, error=false` → red "LLM: Stopped"

### Requirement: Collapsible Bottom Panel

The system SHALL provide a collapsible bottom panel with tabbed views for Events, LLM Sessions, Trace, and Debug.

#### Scenario: Toggle panel

- **GIVEN** the bottom panel is collapsed (height: 0)
- **WHEN** the toggle button is clicked
- **THEN** the panel expands to 180px
- **AND** the Events tab is active by default

#### Scenario: Debug panel

- **GIVEN** the Debug tab is active
- **WHEN** SetAttr form is submitted
- **THEN** a `set_attr` WebSocket message is sent
- **AND** the result appears in the Events tab
