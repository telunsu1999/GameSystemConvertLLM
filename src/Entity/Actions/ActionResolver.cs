using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GameLoop
{
    public class ActionResolver : IActionResolver, IEntityComponent
    {
        private readonly Dictionary<string, IAction> _registry = new Dictionary<string, IAction>();
        private readonly Dictionary<string, ActionMapping> _jsonMappings = new Dictionary<string, ActionMapping>();
        private readonly PrerequisitePipeline _prerequisites = new PrerequisitePipeline();
        private IAction _current;
        private int _startedTick;
        private Dictionary<string, object> _currentParams;

        // When a prerequisite blocks execution, we store the original action here.
        // OnTick schedules it after the resolution action completes.
        private ActionItem _pendingAction;

        // Cached component references from the owning entity (set on each Execute call).
        // Used by OnTick to build ActionContext for continuous actions without
        // taking a dependency on GameEntity.
        private Attributes _lastAttrs;
        private Scheduler _lastScheduler;
        private RecordModule _lastRecords;

        /// <summary>Reference to the global MapSystem. Set by GameLoop after entity creation.</summary>
        public MapSystem Map { get; set; }

        public ActionResolver()
        {
            // Prerequisite pipeline — currently empty (LLM handles movement explicitly).
            // Re-enable by adding: _prerequisites.Add(new LocationPrerequisite());
        }

        // === Config ===

        public void LoadMapping(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, ActionMapping>>>(json);
            if (dict == null || !dict.ContainsKey("actions")) return;

            foreach (var kv in dict["actions"])
            {
                _jsonMappings[kv.Key] = kv.Value;
            }
        }

        public void Register(IAction action)
        {
            _registry[action.ActionType] = action;
        }

        // === State ===

        public CurrentAction Current
        {
            get
            {
                if (_current == null) return null;
                return new CurrentAction
                {
                    ActionType = _current.ActionType,
                    Status = "executing",
                    StartedTick = _startedTick
                };
            }
        }

        // === Execute ===

        public ExecuteResult Execute(ActionItem item, Attributes attrs,
                                      Scheduler scheduler,
                                      TickSnapshot snap, RecordModule records = null)
        {
            // Cache component references for OnTick
            _lastAttrs = attrs;
            _lastScheduler = scheduler;
            _lastRecords = records;

            var ctx = new ActionContext
            {
                Attrs = attrs,
                Records = records,
                Scheduler = scheduler,
                Snap = snap,
                Params = item.Params ?? new Dictionary<string, object>(),
                Map = Map
            };

            // Resolve action
            var action = Resolve(item.ActionType);

            // --- Location check: fail if action requires a location the NPC isn't at ---
            if (_jsonMappings.TryGetValue(item.ActionType, out var mapping)
                && !string.IsNullOrEmpty(mapping.Location))
            {
                var currLoc = attrs.Get("location")?.Value?.ToString();
                if (currLoc != mapping.Location)
                {
                    Logger.Warn("Resolver",
                        $"Execute: {item.ActionType} FAILED (not at {mapping.Location}, currently at {currLoc})",
                        new { tick = snap.Tick });
                    return new ExecuteResult { Completed = false };
                }
            }

            // Check
            if (!action.CanExecute(ctx))
            {
                Logger.Debug("Resolver", $"Execute: {item.ActionType} BLOCKED (check failed)", new { tick = snap.Tick });
                return new ExecuteResult { Completed = false };
            }

            // Execute
            _current = action;
            _startedTick = snap.Tick;
            _currentParams = item.Params;

            var children = action.Execute(ctx).ToList();
            Logger.Info("Resolver", $"Execute: {item.ActionType}", new { tick = snap.Tick, children = children.Count });

            // Submit child actions to scheduler as pre-resolved instances
            foreach (var child in children)
            {
                scheduler.AddResolved(child, child.ActionType, atTick: snap.Tick + 1);
            }

            // If no children AND not a continuous action, clear current
            // Continuous actions stay alive to receive per-tick OnTick calls
            if (children.Count == 0 && !(action is IContinuousAction))
                _current = null;

            return new ExecuteResult
            {
                Completed = true,
                CurrentAction = item.ActionType,
                ChildCount = children.Count
            };
        }

        /// <summary>
        /// Execute a pre-resolved action instance (used for child actions from scheduler).
        /// </summary>
        public ExecuteResult ExecuteResolved(IAction action, Attributes attrs,
            Scheduler scheduler, TickSnapshot snap,
            RecordModule records = null)
        {
            // Cache component references for OnTick
            _lastAttrs = attrs;
            _lastScheduler = scheduler;
            _lastRecords = records;

            var ctx = new ActionContext
            {
                Attrs = attrs,
                Records = records,
                Scheduler = scheduler,
                Snap = snap,
                Params = new Dictionary<string, object>(),
                Map = Map
            };

            if (!action.CanExecute(ctx))
            {
                Logger.Debug("Resolver", $"ExecuteResolved: {action.ActionType} BLOCKED", new { tick = snap.Tick });
                return new ExecuteResult { Completed = false };
            }

            _current = action;
            _startedTick = snap.Tick;

            var children = action.Execute(ctx).ToList();
            Logger.Info("Resolver", $"ExecuteResolved: {action.ActionType}", new { tick = snap.Tick, children = children.Count });

            foreach (var child in children)
            {
                scheduler.AddResolved(child, child.ActionType, atTick: snap.Tick + 1);
            }

            if (children.Count == 0 && !(action is IContinuousAction))
                _current = null;

            return new ExecuteResult
            {
                Completed = true,
                CurrentAction = action.ActionType,
                ChildCount = children.Count
            };
        }

        // === Available Actions ===

        public List<ActionSchema> GetAvailableActions()
        {
            var result = new List<ActionSchema>();
            // JSON-configured actions
            foreach (var (name, mapping) in _jsonMappings)
            {
                result.Add(new ActionSchema
                {
                    ActionType = name,
                    Description = mapping.Description ?? name,
                    Effect = mapping.Effect ?? "",
                    Params = mapping.Params ?? new List<ParamSchema>()
                });
            }
            // Built-in actions (registered via IAction)
            foreach (var (name, _) in _registry)
            {
                // Skip if already in JSON (JSON overrides built-in)
                if (_jsonMappings.ContainsKey(name)) continue;
                result.Add(new ActionSchema
                {
                    ActionType = name,
                    Description = $"内置动作: {name}",
                    Effect = "",
                    Params = new List<ParamSchema>()
                });
            }
            return result;
        }

        // === Interrupt ===

        public void Interrupt(Attributes attrs,
                              Scheduler scheduler, TickSnapshot snap)
        {
            if (_current == null || !_current.Interruptible) return;

            var ctx = new ActionContext
            {
                Attrs = attrs,
                Scheduler = scheduler,
                Snap = snap,
                Params = _currentParams ?? new Dictionary<string, object>(),
                Map = Map
            };

            _current.OnInterrupt(ctx);
            _current = null;
            _currentParams = null;
        }

        // === Internal ===

        private IAction Resolve(string actionType)
        {
            // Map JSON action names to built-in continuous actions
            if (actionType == "goto" || actionType == "move_to")
                return new MoveToAction();

            if (_registry.TryGetValue(actionType, out var action))
                return action;

            if (_jsonMappings.TryGetValue(actionType, out var mapping))
                return new GenericAction(actionType, mapping);

            throw new KeyNotFoundException($"Unknown action type: {actionType}");
        }

        public void OnTick(TickContext ctx)
        {
            var continuous = _current as IContinuousAction;
            if (continuous == null) return;

            var actionCtx = new ActionContext
            {
                Attrs = _lastAttrs,
                Records = _lastRecords,
                Scheduler = _lastScheduler,
                Snap = new TickSnapshot
                {
                    Tick = ctx.Tick,
                    Hour = ctx.Hour,
                    Day = ctx.Day,
                    Season = ctx.Season ?? "",
                    TimeBlock = ctx.TimeBlock ?? ""
                },
                Params = _currentParams ?? new Dictionary<string, object>(),
                Map = Map
            };

            // Always call OnTick (even on completion tick) so movement
            // can yield follow-up actions on the final leg.
            var children = continuous.OnTick(actionCtx).ToList();
            foreach (var child in children)
            {
                _lastScheduler?.AddResolved(child, child.ActionType, atTick: ctx.Tick + 1);
            }

            if (continuous.IsCompleted)
            {
                _current = null;
                _startedTick = 0;
                _currentParams = null;

                // After movement completes, schedule the pending follow-up action
                if (_pendingAction != null)
                {
                    _lastScheduler?.Add(_pendingAction.ActionType,
                        new[] { "auto_follow" },
                        _pendingAction.Params,
                        atTick: ctx.Tick + 1);
                    Logger.Info("Resolver", $"Follow-up scheduled: {_pendingAction.ActionType}",
                        new { tick = ctx.Tick });
                    _pendingAction = null;
                }
            }
        }
    }

    public class ExecuteResult
    {
        public bool Completed;
        public string CurrentAction;
        public int ChildCount;
    }
}
