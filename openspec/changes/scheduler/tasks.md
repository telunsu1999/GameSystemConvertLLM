## 1. Project Setup

- [ ] 1.1 Create `src/Scheduler/Scheduler.csproj` (netstandard2.1)
- [ ] 1.2 Create `tests/Scheduler.Tests/` console test project
- [ ] 1.3 Create `configs/schedules/blacksmith.json` sample config

## 2. Core Models

- [ ] 2.1 Implement `ScheduleItem` class (ActionType, Tags, Params, TriggerTick, Interval)
- [ ] 2.2 Implement `ScheduleConfig` JSON model for file loading

## 3. Scheduler Core

- [ ] 3.1 Implement internal priority queue (sorted by TriggerTick)
- [ ] 3.2 Implement `Add(actionType, tags, params, timing)` with validation
- [ ] 3.3 Implement `DueNow(tick)` — return due items without consuming
- [ ] 3.4 Implement `Upcoming(count)` — preview next N items
- [ ] 3.5 Implement `GetByTags(tags)` — tag-based query
- [ ] 3.6 Implement `Remove(actionType)` and `Clear()`

## 4. Check Mechanism

- [ ] 4.1 Implement `Check(snap)` — pop due items, re-enqueue repeats, return triggered items
- [ ] 4.2 Verify Check does NOT execute actions (only returns items)

## 5. Config Loading

- [ ] 5.1 Implement `LoadConfig(jsonPath)` — load from JSON file
- [ ] 5.2 Create `configs/schedules/blacksmith.json` sample config
- [ ] 5.3 Create `configs/schedules/forest_zone.json` sample config (spawn)

## 6. Tests

- [ ] 6.1 Test Add with various timing modes (atTick, atHour, everyTick, everyHour)
- [ ] 6.2 Test DueNow correctness
- [ ] 6.3 Test Upcoming ordering and limit
- [ ] 6.4 Test GetByTags
- [ ] 6.5 Test Check pops and re-enqueues repeating items
- [ ] 6.6 Test Remove and Clear
- [ ] 6.7 Test LoadConfig
- [ ] 6.8 Test integration with WorldClock events (OnHourChanged subscription)
