## 1. Setup

- [x] 1.1 Create `data_collector/DataCollector/` project with all references
- [x] 1.2 Move rules to `configs/semantic/`

## 2. Core

- [x] 2.1 Implement CollectConfig and CollectResult models
- [x] 2.2 Implement DataCollector.Collect(configName) �?config-driven
- [x] 2.3 Implement DataCollector.Collect(config) �?direct API
- [x] 2.4 Implement DataCollector.CollectRaw()
- [x] 2.5 Implement event flattening (Event �?memory_event dict)

## 3. Configs

- [x] 3.1 Create sample `configs/collect/dungeon_scene.json`

## 4. Tests

- [x] 4.1 Test CollectRaw returns attrs + events
- [x] 4.2 Test Collect returns semantic texts
- [x] 4.3 Test config file loading
- [x] 4.4 Test missing config throws
- [x] 4.5 Test event flattening format
