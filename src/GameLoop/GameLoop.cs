using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// Game driver (Actor pattern). Holds all entities, clock, events.
    /// On each tick: creates TickContext, calls entity.OnTick (which iterates components),
    /// then processes scheduled actions and directs post-tick orchestration.
    /// </summary>
    public class GameLoop
    {
        private readonly WorldClock _clock;
        private readonly WorldLog _worldLog;
        private readonly Dictionary<string, GameEntity> _entities = new Dictionary<string, GameEntity>();
        private readonly object _autoTickLock = new object();
        private Timer _autoTickTimer;
        private Action<StateSnapshot> _autoTickCallback;
        private bool _llmEnabled;
        private MapSystem _mapSystem;

        public WorldClock Clock => _clock;
        public WorldLog WorldLog => _worldLog;
        public IReadOnlyDictionary<string, GameEntity> Entities => _entities;
        public bool LlmEnabled { get => _llmEnabled; set => _llmEnabled = value; }
        public MapSystem Map => _mapSystem;

        // --- Auto-tick state ---
        public bool AutoTickRunning { get; private set; }
        public int AutoTickIntervalMs { get; private set; } = 2000;
        public bool BlockOnLlmThinking { get; set; } = false;
        
        /// <summary>Fires when any entity attribute changes. (entityId, key, value)</summary>
        public event Action<string, string, object> OnNpcAttrChanged;
        
        /// <summary>Fires when an LLM plan session completes (success or error).</summary>
        public event Action<LlmSession> OnPlanSession;
        
        /// <summary>Fires when any NPC goal milestone completes.</summary>
        public event Action<string> OnGoalProgressChanged;

        public GameLoop(WorldClock clock, WorldLog worldLog = null)
        {
            _clock = clock;
            _worldLog = worldLog ?? new WorldLog();
        }

        /// <summary>
        /// Load a grid map from JSON config and initialise the navigation graph.
        /// Call before LoadEntities so entity positions are registered correctly.
        /// </summary>
        public void LoadMap(string mapConfigPath)
        {
            var json = File.ReadAllText(mapConfigPath);
            var config = JsonConvert.DeserializeObject<MapGridConfig>(json);
            if (config == null)
                throw new InvalidOperationException($"Failed to load map config: {mapConfigPath}");

            _mapSystem = new MapSystem();
            _mapSystem.Load(config);
            Logger.Info("GameLoop", $"Map loaded: {_mapSystem.MapName}");
        }

        public void AddEntity(GameEntity entity)
        {
            _entities[entity.Id] = entity;
            var attrs = entity.Get<Attributes>();
            if (attrs != null)
                attrs.OnChanged += (key, val) => OnNpcAttrChanged?.Invoke(entity.Id, key, val);

            // Wire MapSystem to resolver
            var resolver = entity.Get<ActionResolver>();
            if (resolver != null && _mapSystem != null)
                resolver.Map = _mapSystem;

            // Register entity position in map
            if (_mapSystem != null)
            {
                var loc = entity.Str("location");
                if (!string.IsNullOrEmpty(loc))
                    _mapSystem.RegisterEntity(entity.Id, loc);
            }
        }

        /// <summary>
        /// Load all entities from JSON configs in a directory.
        /// Each entity gets a LlmPlanner component attached.
        /// </summary>
        public void LoadEntities(string entityDir, string rootDir, LLMClient llmClient)
        {
            if (!System.IO.Directory.Exists(entityDir)) return;
            foreach (var file in System.IO.Directory.GetFiles(entityDir, "*.json"))
            {
                var entity = EntityFactory.CreateFromJson(file, rootDir);
                entity.Add(new LlmPlanner(llmClient, rootDir, 
                    (session) => OnPlanSession?.Invoke(session)));
                // Wire goal progress to event
                var goals = entity.Get<GoalComponent>();
                if (goals != null)
                    goals.OnMilestoneCompleted += (g, m) => OnGoalProgressChanged?.Invoke(entity.Id);
                AddEntity(entity);
            }
            Logger.Info("GameLoop", $"Loaded {_entities.Count} entities");
        }

        public GameEntity? GetEntity(string id)
        {
            _entities.TryGetValue(id, out var e);
            return e;
        }

        /// <summary>
        /// Advance game time by N ticks. Calls callback with state after each tick batch.
        /// </summary>
        public void AdvanceTicks(int count, Action<StateSnapshot> onStateChanged)
        {
            var oldSnap = _clock.Snapshot();
            for (int i = 0; i < count; i++)
            {
                _clock.Advance(1);
                var snap = _clock.Snapshot();
                var llmTasks = new List<Task>();

                foreach (var (id, entity) in _entities)
                {
                    // Build tick context and let entity drive its components
                    var ctx = new TickContext
                    {
                        Tick = snap.Tick, Hour = snap.Hour, Day = snap.Day,
                        Season = snap.Season, TimeBlock = snap.TimeBlock,
                        LlmEnabled = _llmEnabled
                    };
                    entity.OnTick(ctx);

                    // Process scheduled actions
                    var sched = entity.Get<Scheduler>();
                    var resolver = entity.Get<ActionResolver>();
                    var attrs = entity.Get<Attributes>();
                    var records = entity.Get<RecordModule>();
                    if (sched != null && resolver != null && attrs != null)
                    {
                        var due = sched.Check(snap);
                        foreach (var item in due)
                        {
                            if (item.ResolvedAction is IAction resolvedAct)
                                resolver.ExecuteResolved(resolvedAct, attrs, sched, snap, records);
                            else
                                resolver.Execute(new ActionItem { ActionType = item.ActionType, Params = item.Params },
                                    attrs, sched, snap, records);
                            Logger.Debug("Tick", $"schedule: {id}.{item.ActionType}", new { tick = snap.Tick });
                        }
                    }
                }

                Task.WaitAll(llmTasks.ToArray());
                oldSnap = snap;
            }

            onStateChanged(BuildState());
        }

        // ========== Auto-Tick ==========

        /// <summary>Start automatic tick loop. Fires onStateChanged after each tick.</summary>
        public void StartAutoTick(int intervalMs, Action<StateSnapshot> onStateChanged)
        {
            lock (_autoTickLock)
            {
                StopAutoTick();
                AutoTickIntervalMs = intervalMs;
                _autoTickCallback = onStateChanged;
                AutoTickRunning = true;
                _autoTickTimer = new Timer(_ =>
                {
                    if (!AutoTickRunning) return;
                    // If blocking mode and any NPC is thinking, skip this tick
                    if (BlockOnLlmThinking && AnyNpcThinking()) return;
                    AdvanceTicks(1, _autoTickCallback);
                }, null, 0, intervalMs);
            }
        }

        /// <summary>Stop the auto-tick loop.</summary>
        public void StopAutoTick()
        {
            lock (_autoTickLock)
            {
                AutoTickRunning = false;
                _autoTickTimer?.Dispose();
                _autoTickTimer = null;
            }
        }

        /// <summary>Change auto-tick interval. Takes effect on next tick.</summary>
        public void SetAutoTickInterval(int intervalMs)
        {
            AutoTickIntervalMs = intervalMs;
            if (AutoTickRunning && _autoTickTimer != null)
                _autoTickTimer.Change(0, intervalMs);
        }

        /// <summary>Check if any NPC currently has llm_thinking == 1.</summary>
        public bool AnyNpcThinking()
        {
            foreach (var (_, entity) in _entities)
            {
                if (entity.Int("llm_thinking") == 1) return true;
            }
            return false;
        }

        // ==============================

        /// <summary>
        /// Player interaction with a specific entity.
        /// </summary>
        public void Interact(string entityId, Action<StateSnapshot> onStateChanged)
        {
            var entity = _entities.GetValueOrDefault(entityId);
            if (entity == null) return;
            var snap = _clock.Snapshot();

            var attrs = entity.Get<Attributes>();
            var triggers = entity.Get<TriggerSystem>();
            var resolver = entity.Get<ActionResolver>();
            var sched = entity.Get<Scheduler>();
            if (attrs == null || triggers == null || resolver == null || sched == null) return;

            attrs.Set("player_near", 1, "number", new[] { "social" });
            var res = triggers.Evaluate("attr", "player_near", attrs);
            attrs.Set("player_near", 0, "number", new[] { "social" });

            foreach (var r in res.DirectActions)
            {
                resolver.Execute(new ActionItem { ActionType = r.ActionType, Params = r.Params },
                    attrs, sched, snap, entity.Get<RecordModule>());
            }

            onStateChanged(BuildState());
        }

        /// <summary>
        /// Build a serializable state snapshot for UI.
        /// </summary>
        public StateSnapshot BuildState()
        {
            var snap = _clock.Snapshot();
            var npcs = new Dictionary<string, object>();
            foreach (var (id, entity) in _entities)
            {
                npcs[id] = new
                {
                    name = entity.Str("name"), hp = entity.Int("hp"), hpMax = entity.Int("hp_max"),
                    stamina = entity.Int("stamina"), gold = entity.Int("gold"),
                    location = entity.Str("location"),
                    moving = entity.Int("moving"), dead = entity.Int("dead"),
                    ironOwned = entity.Int("iron_owned"), foodOwned = entity.Int("food_owned"),
                    personality = entity.Str("personality"),
                    allAttrs = GameLoopHelpers.BuildAttrSnapshot(entity),
                    upcoming = entity.Get<Scheduler>()?.Upcoming(5).Select(u => new
                    {
                        tick = u.TriggerTick, action = u.ActionType,
                        detail = BuildUpcomingDetail(u)
                    }).ToList(),
                    currentAction = entity.Get<ActionResolver>()?.Current?.ActionType ?? "idle",
                    llmThinking = entity.Int("llm_thinking") == 1,
                    goals = entity.Get<GoalComponent>()?.Goals.Select(g => new
                    {
                        id = g.Id, desc = g.Desc,
                        type = g.Type == GoalType.LongTerm ? "long" : "short",
                        progress = (int)(g.Progress * 100),
                        milestone = g.CurrentMilestone?.Desc ?? "",
                        milestoneDone = g.Milestones.Count(m => m.Done),
                        milestoneTotal = g.Milestones.Count,
                        completed = g.Completed,
                        priority = g.Priority
                    }).ToList()
                };
            }
            return new StateSnapshot
            {
                Tick = snap.Tick, Year = snap.Year, Month = snap.Month,
                Day = snap.Day, Hour = snap.Hour, Minute = snap.Minute,
                Second = snap.Second, Season = snap.Season,
                TimeBlock = snap.TimeBlock, Npcs = npcs
            };
        }

        private static string BuildUpcomingDetail(ScheduleItem u)
        {
            var icon = u.ActionType switch
            {
                "goto" => "🚶", "talk" => "💬", "work" => "🔨", "drink" => "🍺",
                "pray" => "🙏", "eat" => "🍖", "defend" => "🛡️", "greet" => "👋",
                "sleep" => "😴", "wake" => "🌅", "wander" => "🚶", "rest" => "💤",
                "heal" => "💊", "attack" => "⚔️", _ => ""
            };
            var parts = new System.Collections.Generic.List<string>();
            if (u.Params.TryGetValue("target", out var t) && t != null && t.ToString().Length > 0)
                parts.Add($"→{t}");
            if (u.Params.TryGetValue("income", out var i) && i != null)
                parts.Add($"💰{i}");
            if (u.Params.TryGetValue("location", out var l) && l != null && l.ToString().Length > 0)
                parts.Add($"📍{l}");
            var detail = string.Join(" ", parts);
            return string.IsNullOrEmpty(detail) ? icon : $"{icon} {detail}";
        }

        /// <summary>
        /// Build serializable map data for the frontend. Returns null if no map is loaded.
        /// </summary>
        public object? BuildMapData()
        {
            if (_mapSystem == null) return null;

            // Build terrain grid as list of strings (compact)
            var grid = new List<string>();
            var terrainMap = new Dictionary<string, object>();
            var terrainColors = new Dictionary<string, string>
            {
                ["草地"] = "#4a7c3f", ["道路"] = "#c4a45a", ["水域"] = "#3a6b9e",
                ["建筑"] = "#6b5a4b", ["森林"] = "#2d5a1e", ["山脉"] = "#5a5a5a",
                ["边界"] = "#1a1a2e"
            };

            for (int y = 0; y < _mapSystem.Height; y++)
            {
                var row = new char[_mapSystem.Width];
                for (int x = 0; x < _mapSystem.Width; x++)
                {
                    var t = _mapSystem.GetTerrain(x, y);
                    row[x] = t.Walkable ? (t.SpeedMod >= 1.5 ? 'R' : t.SpeedMod >= 1.0 ? 'G' : 'F') :
                             t.Name == "水域" ? 'W' : t.Name == "建筑" ? 'B' : 'M';

                    if (!terrainMap.ContainsKey(t.Name))
                    {
                        terrainMap[t.Name] = new
                        {
                            name = t.Name,
                            walkable = t.Walkable,
                            speedMod = t.SpeedMod,
                            color = terrainColors.GetValueOrDefault(t.Name, "#444")
                        };
                    }
                }
                grid.Add(new string(row));
            }

            // Nodes
            var nodes = new List<object>();
            foreach (var kv in _mapSystem.Nodes)
            {
                var n = kv.Value;
                nodes.Add(new
                {
                    id = n.Id, name = n.Name,
                    gridX = n.GridX, gridY = n.GridY,
                    type = n.Type, tags = n.Tags
                });
            }

            // Entity positions
            var entities = new List<object>();
            foreach (var kv in _mapSystem.EntityLocations)
            {
                var node = _mapSystem.Nodes.TryGetValue(kv.Value, out var nd) ? nd : null;
                entities.Add(new
                {
                    entityId = kv.Key,
                    nodeId = kv.Value,
                    gridX = node?.GridX ?? 0,
                    gridY = node?.GridY ?? 0
                });
            }

            return new
            {
                mapId = _mapSystem.MapId,
                mapName = _mapSystem.MapName,
                width = _mapSystem.Width,
                height = _mapSystem.Height,
                grid = grid,
                terrains = terrainMap.Values.ToList(),
                nodes = nodes,
                entities = entities
            };
        }
    }

    public class StateSnapshot
    {
        public int Tick; public int Year; public int Month;
        public int Day; public int Hour; public int Minute; public int Second;
        public string Season; public string TimeBlock;
        public Dictionary<string, object> Npcs = new Dictionary<string, object>();
    }

    internal static class GameLoopHelpers
    {
        public static Dictionary<string, object> BuildAttrSnapshot(GameEntity entity)
        {
            var result = new Dictionary<string, object>();
            var attrs = entity.Get<Attributes>()?.Export();
            if (attrs == null) return result;
            foreach (var kv in attrs)
                result[kv.Key] = new { v = kv.Value.Value, t = kv.Value.Tags };
            return result;
        }
    }
}
