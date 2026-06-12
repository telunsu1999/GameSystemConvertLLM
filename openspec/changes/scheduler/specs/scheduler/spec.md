## ADDED Requirements

### Requirement: Schedule item registration
The system SHALL allow adding schedule items with action type, tags, params, and timing.

#### Scenario: Add one-time action at specific tick
- **WHEN** `Add("goto", tags:["daily"], params:{location:"workshop"}, atHour:6)` is called
- **THEN** a ScheduleItem is enqueued with ActionType="goto", TriggerTick calculated for hour 6

#### Scenario: Add repeating action
- **WHEN** `Add("spawn", tags:["combat"], params:{monster:"slime"}, everyTick:12)` is called
- **THEN** the item has Interval=12 and will repeat after each trigger

#### Scenario: Add action with no timing
- **WHEN** `Add("idle", tags:[], params:{})` is called without any timing parameter
- **THEN** an exception is thrown

### Requirement: Due action retrieval
The system SHALL return scheduled items whose TriggerTick equals or precedes the given tick.

#### Scenario: Item due now
- **WHEN** an item has TriggerTick=72 and `DueNow(72)` is called
- **THEN** the item is returned in the result list

#### Scenario: Item not yet due
- **WHEN** an item has TriggerTick=100 and `DueNow(72)` is called
- **THEN** the item is NOT returned

#### Scenario: Item already triggered
- **WHEN** an item has TriggerTick=50 and `DueNow(72)` is called for a one-time item
- **THEN** the item is NOT returned (was consumed when DueNow(50) was called)

### Requirement: Upcoming query
The system SHALL return the next N scheduled items without removing them.

#### Scenario: Upcoming returns sorted items
- **WHEN** items exist at ticks 72, 108, 144 and `Upcoming(2)` is called
- **THEN** items at 72 and 108 are returned

#### Scenario: Upcoming limited by count
- **WHEN** 5 items exist and `Upcoming(3)` is called
- **THEN** exactly 3 items are returned

### Requirement: Tag-based query
The system SHALL support querying schedule items by tags.

#### Scenario: Query by single tag
- **WHEN** items with tags ["daily","work"] and ["combat"] exist
- **THEN** `GetByTags(["daily"])` returns only the "daily" tagged item

#### Scenario: Query by multiple tags
- **WHEN** items with tags ["daily","work"] and ["combat"] and ["daily","rest"] exist
- **THEN** `GetByTags(["daily","work"])` returns both daily-tagged items

### Requirement: Item removal
The system SHALL allow removing specific schedule items by action type.

#### Scenario: Remove existing item
- **WHEN** `Remove("goto")` is called and a "goto" item exists
- **THEN** the item is removed from the queue

#### Scenario: Remove non-existent item
- **WHEN** `Remove("nonexistent")` is called
- **THEN** no error occurs, queue unchanged

### Requirement: Clear all items
The system SHALL support clearing all scheduled items.

#### Scenario: Clear removes everything
- **WHEN** `Clear()` is called on a Scheduler with 5 items
- **THEN** `Upcoming(10)` returns empty list

### Requirement: Check drives the queue
The system SHALL provide a `Check(snap)` method that pops due items and adds them to a result list.

#### Scenario: Check pops due items
- **WHEN** item at TriggerTick=72 exists and `Check(snap.Tick=72)` is called
- **THEN** the item is returned in the result list and removed from the queue (if one-time)

#### Scenario: Repeating item re-enqueues
- **WHEN** repeating item triggers at tick 72 with Interval=144
- **THEN** after Check(72), a new item exists with TriggerTick=216

### Requirement: JSON config loading
The system SHALL load schedule items from JSON configuration files.

#### Scenario: Load valid config
- **WHEN** `LoadConfig("configs/schedules/blacksmith.json")` is called
- **THEN** schedule items from the file are added to the Scheduler
