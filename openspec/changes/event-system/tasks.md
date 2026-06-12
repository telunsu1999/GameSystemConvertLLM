## 1. Project Setup

- [x] 1.1 Create `event_system/EventSystem/` class library (netstandard2.1)
- [x] 1.2 Create `event_system/EventScorer/` class library (netstandard2.1)

## 2. Core Models

- [x] 2.1 Implement `Event` class (Id/Type/Tags/Time/Data/Perceptions)
- [x] 2.2 Implement `Perception` class (NpcId/When/How/From)
- [x] 2.3 Implement `EventQuery` class (all filter fields with defaults)

## 3. EventSystem

- [x] 3.1 Implement Record(type, tags, data) â†?string (GUID)
- [x] 3.2 Implement Perceive(eventId, npcId, how, from?)
- [x] 3.3 Implement Spread(eventId, fromNpc, toNpc, how)
- [x] 3.4 Implement Query(EventQuery) with tag index

## 4. EventScorer

- [x] 4.1 Define IScoringStrategy interface
- [x] 4.2 Implement DefaultScorer (type weights + recency decay)

## 5. Tests

- [x] 5.1 Test Record returns ID and event is stored
- [x] 5.2 Test Perceive makes event visible to NPC
- [x] 5.3 Test Spread propagates knowledge
- [x] 5.4 Test Query with NPC filter
- [x] 5.5 Test Query with Tags filter
- [x] 5.6 Test Query with RecentDays
- [x] 5.7 Test Query with SourceFilter
- [x] 5.8 Test Query sorting (time_desc/time_asc/score_desc)
- [x] 5.9 Test Query limit
- [x] 5.10 Test multi-dimension combination
- [x] 5.11 Test default scorer (death > weather, recent > old)
