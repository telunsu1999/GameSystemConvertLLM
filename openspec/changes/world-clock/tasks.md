## 1. Project Setup

- [ ] 1.1 Create `src/WorldClock/WorldClock.csproj` (netstandard2.1, zero dependencies)
- [ ] 1.2 Create `tests/WorldClock.Tests/` console test project
- [ ] 1.3 Create `configs/clock/world_clock.json` with default Stardew-like config

## 2. Core Models

- [ ] 2.1 Implement `TickSnapshot` struct (Tick, Minute, Hour, Day, Season, Year, TimeBlock)
- [ ] 2.2 Implement time configuration model (`ClockConfig`) deserialized from JSON

## 3. WorldClock Core

- [ ] 3.1 Implement Tick property (get-only) and `Advance(int ticks)` method
- [ ] 3.2 Implement derived properties: Minute, Hour, Day, Season, Year
- [ ] 3.3 Implement TimeBlock derivation from configured time_blocks
- [ ] 3.4 Implement `FromConfig(jsonPath)` static factory

## 4. Event System

- [ ] 4.1 Implement event delegates: OnTick, OnTimeBlockChanged, OnHourChanged, OnDayChanged, OnSeasonChanged, OnYearChanged
- [ ] 4.2 Implement event firing logic in Advance() with correct ordering (fine→coarse)
- [ ] 4.3 Implement `Snapshot()` method returning current TickSnapshot

## 5. Configuration

- [ ] 5.1 Implement config loading with JSON validation (time_blocks coverage, days_per_season > 0)
- [ ] 5.2 Create `configs/clock/world_clock.json` with 7 time_blocks and 4 seasons

## 6. Tests

- [ ] 6.1 Test Tick advancement and derived property correctness
- [ ] 6.2 Test Season and Year boundary transitions
- [ ] 6.3 Test TimeBlock derivation at all boundaries
- [ ] 6.4 Test event firing order when crossing multiple boundaries
- [ ] 6.5 Test Snapshot captures correct state at event time
- [ ] 6.6 Test FromConfig with valid and invalid config files
