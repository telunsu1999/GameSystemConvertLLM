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
        private IAction _current;
        private int _startedTick;

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
            var ctx = new ActionContext
            {
                Attrs = attrs,
                Records = records,
                Scheduler = scheduler,
                Snap = snap,
                Params = item.Params ?? new Dictionary<string, object>()
            };

            // Resolve action
            var action = Resolve(item.ActionType);

            // Check
            if (!action.CanExecute(ctx))
            {
                Logger.Debug("Resolver", $"Execute: {item.ActionType} BLOCKED (check failed)", new { tick = snap.Tick });
                return new ExecuteResult { Completed = false };
            }

            // Execute
            _current = action;
            _startedTick = snap.Tick;

            var children = action.Execute(ctx).ToList();
            Logger.Info("Resolver", $"Execute: {item.ActionType}", new { tick = snap.Tick, children = children.Count });

            // Submit child actions to scheduler as pre-resolved instances
            foreach (var child in children)
            {
                scheduler.AddResolved(child, child.ActionType, atTick: snap.Tick + 1);
            }

            // If no children (self-contained action), clear current
            if (children.Count == 0)
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
            var ctx = new ActionContext
            {
                Attrs = attrs,
                Records = records,
                Scheduler = scheduler,
                Snap = snap,
                Params = new Dictionary<string, object>()
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

            if (children.Count == 0)
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
            foreach (var (name, mapping) in _jsonMappings)
            {
                var schema = new ActionSchema
                {
                    ActionType = name,
                    Description = mapping.Description ?? name,
                    Effect = mapping.Effect ?? "",
                    Params = mapping.Params ?? new List<ParamSchema>()
                };
                result.Add(schema);
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
                Params = new Dictionary<string, object>()
            };

            _current.OnInterrupt(ctx);
            _current = null;
        }

        // === Internal ===

        private IAction Resolve(string actionType)
        {
            if (_registry.TryGetValue(actionType, out var action))
                return action;

            if (_jsonMappings.TryGetValue(actionType, out var mapping))
                return new GenericAction(actionType, mapping);

            throw new KeyNotFoundException($"Unknown action type: {actionType}");
        }

        public void OnTick(TickContext ctx) { }
    }

    public class ExecuteResult
    {
        public bool Completed;
        public string CurrentAction;
        public int ChildCount;
    }
}
