## ADDED Requirements

### Requirement: Rule registration from JSON config
The system SHALL load trigger rules from a JSON configuration file.

#### Scenario: Load valid config
- **WHEN** `LoadRules("configs/triggers/guard.json")` is called
- **THEN** rules from the file are loaded into the TriggerSystem

#### Scenario: Load missing config
- **WHEN** `LoadRules("nonexistent.json")` is called
- **THEN** an exception is thrown

### Requirement: Passive evaluation
The system SHALL provide an `Evaluate()` method that returns matching rules without internal state changes.

#### Scenario: Evaluate returns matching rules
- **WHEN** a rule with condition {field:"hp", op:"lt", value:20} exists
- **THEN** `Evaluate("attr", "hp", snap, attrs, events)` returns it when attrs.Get("hp") < 20

#### Scenario: Evaluate returns empty when no match
- **WHEN** no rule's conditions match current state
- **THEN** `Evaluate()` returns TriggerResult with empty lists

#### Scenario: Evaluate is stateless
- **WHEN** `Evaluate()` is called twice with same state
- **THEN** both calls return identical results

### Requirement: Direct mode rules
The system SHALL classify mode="direct" rules into TriggerResult.DirectActions.

#### Scenario: Direct rule returned in DirectActions
- **WHEN** a rule with mode="direct" matches
- **THEN** it appears in TriggerResult.DirectActions

### Requirement: LLM mode rules
The system SHALL classify mode="llm" rules into TriggerResult.LlmOptions.

#### Scenario: LLM rule returned in LlmOptions
- **WHEN** a rule with mode="llm" matches
- **THEN** it appears in TriggerResult.LlmOptions

#### Scenario: Description field for LLM prompt
- **WHEN** a rule has description="打听小道消息"
- **THEN** the description is accessible in TriggerResult.LlmOptions[i].Description

### Requirement: Condition operators
The system SHALL support equality and comparison operators in conditions.

#### Scenario: Equality match
- **WHEN** condition is {field:"location", op:"eq", value:"酒馆"}
- **THEN** it matches when attrs.Get("location").Value equals "酒馆"

#### Scenario: Greater than match
- **WHEN** condition is {field:"hp", op:"gt", value:50}
- **THEN** it matches when attrs.Get("hp") value > 50

#### Scenario: Less than match
- **WHEN** condition is {field:"hp", op:"lt", value:20}
- **THEN** it matches when attrs.Get("hp") value < 20

#### Scenario: Greater than or equal match
- **WHEN** condition is {field:"hp", op:"gte", value:50}
- **THEN** it matches when attrs.Get("hp") value >= 50

#### Scenario: Less than or equal match
- **WHEN** condition is {field:"hp", op:"lte", value:100}
- **THEN** it matches when attrs.Get("hp") value <= 100

### Requirement: Tags on rules
The system SHALL support tags on each rule for external filtering.

#### Scenario: Tags accessible
- **WHEN** a rule has tags ["combat","emergency"]
- **THEN** the tags are accessible in the returned TriggerRule
