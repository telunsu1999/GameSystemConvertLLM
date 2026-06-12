using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly EventSystem _events;
        private readonly Dictionary<string, GameEntity> _entities = new Dictionary<string, GameEntity>();
        private bool _llmEnabled;

        public WorldClock Clock => _clock;
        public EventSystem Events => _events;
        public IReadOnlyDictionary<string, GameEntity> Entities => _entities;
        public bool LlmEnabled { get => _llmEnabled; set => _llmEnabled = value; }
        
        /// <summary>Fires when any entity attribute changes. (entityId, key, value)</summary>
        public event Action<string, string, object> OnNpcAttrChanged;
        
        /// <summary>Fires when an LLM plan session completes (success or error).</summary>
        public event Action<LlmSession> OnPlanSession;

        public GameLoop(WorldClock clock, EventSystem events)
        {
            _clock = clock;
            _events = events;
        }

        public void AddEntity(GameEntity entity)
        {
            _entities[entity.Id] = entity;
            var attrs = entity.Get<Attributes>();
            if (attrs != null)
                attrs.OnChanged += (key, val) => OnNpcAttrChanged?.Invoke(entity.Id, key, val);
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
                        Events = _events, LlmEnabled = _llmEnabled
                    };
                    entity.OnTick(ctx);

                    // Process scheduled actions
                    var sched = entity.Get<Scheduler>();
                    var resolver = entity.Get<ActionResolver>();
                    var attrs = entity.Get<Attributes>();
                    if (sched != null && resolver != null && attrs != null)
                    {
                        var due = sched.Check(snap);
                        foreach (var item in due)
                        {
                            if (item.ResolvedAction is IAction resolvedAct)
                                resolver.ExecuteResolved(resolvedAct, attrs, _events, sched, snap);
                            else
                                resolver.Execute(new ActionItem { ActionType = item.ActionType, Params = item.Params },
                                    attrs, _events, sched, snap);
                            Logger.Debug("Tick", $"schedule: {id}.{item.ActionType}", new { tick = snap.Tick });
                        }
                    }
                }

                Task.WaitAll(llmTasks.ToArray());
                oldSnap = snap;
            }

            onStateChanged(BuildState());
        }

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
                    attrs, _events, sched, snap);
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
                    upcoming = entity.Get<Scheduler>()?.Upcoming(3).Select(u => new
                    {
                        tick = u.TriggerTick, action = u.ActionType,
                        detail = u.Params.ContainsKey("location") ? u.Params["location"].ToString() : ""
                    }).ToList(),
                    currentAction = entity.Get<ActionResolver>()?.Current?.ActionType ?? "idle",
                    llmThinking = entity.Int("llm_thinking") == 1
                };
            }
            return new StateSnapshot
            {
                Tick = snap.Tick, Season = snap.Season, Day = snap.Day,
                TimeBlock = snap.TimeBlock, Hour = snap.Hour, Npcs = npcs
            };
        }
    }

    public class StateSnapshot
    {
        public int Tick; public string Season; public int Day;
        public string TimeBlock; public int Hour;
        public Dictionary<string, object> Npcs = new Dictionary<string, object>();
    }
}
