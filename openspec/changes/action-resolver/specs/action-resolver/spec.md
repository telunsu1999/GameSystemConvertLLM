## ADDED Requirements

### Requirement: IAction interface
The system SHALL define an `IAction` interface with CanExecute, Execute, and OnInterrupt methods.

#### Scenario: Action can check preconditions
- **WHEN** `CanExecute(ctx)` is called with insufficient conditions
- **THEN** it returns false

#### Scenario: Execute returns child actions
- **WHEN** a goto Action executes
- **THEN** it yields an ArriveAction as child

#### Scenario: OnInterrupt handles cancellation
- **WHEN** `OnInterrupt(ctx)` is called on an executing Action
- **THEN** the Action cleans up its state

### Requirement: ActionResolver dispatch
The system SHALL provide an ActionResolver that dispatches Action execution.

#### Scenario: Execute dispatches to registered Action
- **WHEN** `Execute("goto", params, ctx)` is called
- **THEN** the registered GotoAction handles execution

#### Scenario: Execute falls back to GenericAction from JSON
- **WHEN** `Execute("ignore", params, ctx)` is called and no C# Action is registered
- **THEN** a GenericAction built from JSON mapping handles it

### Requirement: JSON action mapping
The system SHALL load action mappings from JSON configuration files.

#### Scenario: Load valid mapping
- **WHEN** `LoadMapping("configs/actions/guard.json")` is called
- **THEN** actions defined in the file are loaded

### Requirement: Step-based GenericAction
The system SHALL support GenericAction built from JSON step sequences.

#### Scenario: set_attr step
- **WHEN** GenericAction has step type="set_attr" with field="moving" value=1
- **THEN** attrs.Set("moving", 1) is called

#### Scenario: record_event step
- **WHEN** GenericAction has step type="record_event"
- **THEN** events.Record(...) is called with configured parameters

#### Scenario: Template substitution
- **WHEN** step value is "{params.location}"
- **THEN** the actual value from ActionItem.Params["location"] is used

### Requirement: Interrupt support
The system SHALL support interrupting the currently executing Action.

#### Scenario: Interruptible action interrupted
- **WHEN** `Interrupt(ctx)` is called on an interruptible Action
- **THEN** OnInterrupt is called and Current becomes null

#### Scenario: Non-interruptible action
- **WHEN** `Interrupt(ctx)` is called on a non-interruptible Action
- **THEN** the Action continues executing

### Requirement: Current state exposure
The system SHALL expose the currently executing Action's state.

#### Scenario: Current returns executing Action
- **WHEN** an Action is executing
- **THEN** `Current` returns non-null with ActionType, Params, Status

#### Scenario: Current is null when idle
- **WHEN** no Action is executing
- **THEN** `Current` returns null

### Requirement: LLM parameter schema
The system SHALL support params_schema on Action mappings for LLM parameter filling.

#### Scenario: select-level action
- **WHEN** an Action has level="select" with params_schema
- **THEN** the schema defines which parameters LLM must choose from options

#### Scenario: freeform-level action
- **WHEN** an Action has level="freeform" with params_schema
- **THEN** the schema defines which parameters LLM freely generates
