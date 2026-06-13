using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GameLoop
{
    /// <summary>
    /// Creates GameEntity instances from JSON config or via code registration.
    ///
    /// JSON path: loads EntityConfig, then resolves relative paths for
    /// schedule/triggers/actions based on a root directory.
    ///
    /// Code path: creates entity, caller registers actions/schedules/triggers manually.
    /// </summary>
    public static class EntityFactory
    {
        /// <summary>
        /// Create entity from JSON config file.
        /// </summary>
        public static GameEntity CreateFromJson(string configPath, string rootDir)
        {
            var json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<EntityConfig>(json);
            if (config == null)
                throw new InvalidOperationException($"Failed to load entity config: {configPath}");

            var entity = new GameEntity(config.Id);

            // Add and init Attributes
            var attrs = entity.Add(new Attributes());
            foreach (var a in config.Attrs)
                attrs.Set(a.Key, a.Value, a.Type, a.Tags ?? new string[0]);

            // Add Scheduler + load config
            if (!string.IsNullOrEmpty(config.SchedulePath))
            {
                var sched = entity.Add(new Scheduler());
                var schedulePath = ResolvePath(config.SchedulePath, rootDir);
                if (File.Exists(schedulePath)) sched.LoadConfig(schedulePath);
            }

            // Add Triggers + load rules
            if (!string.IsNullOrEmpty(config.TriggersPath))
            {
                var triggers = entity.Add(new TriggerSystem());
                var triggersPath = ResolvePath(config.TriggersPath, rootDir);
                if (File.Exists(triggersPath)) triggers.LoadRules(triggersPath);
            }

            // Add ActionResolver + load mappings + register core actions
            var resolver = entity.Add(new ActionResolver());
            foreach (var actionPath in config.ActionPaths)
            {
                var resolved = ResolvePath(actionPath, rootDir);
                if (File.Exists(resolved)) resolver.LoadMapping(resolved);
            }
            resolver.Register(new GotoAction());
            resolver.Register(new ArriveAction());
            resolver.Register(new TakeDamageAction());
            resolver.Register(new TradeAction());

            // Add RecordModule (per-entity memory, always present)
            entity.Add(new RecordModule());

            // Add RelationModule + load relations
            if (!string.IsNullOrEmpty(config.RelationsPath))
            {
                var rel = entity.Add(new RelationModule());
                var relPath = ResolvePath(config.RelationsPath, rootDir);
                if (File.Exists(relPath))
                {
                    var relJson = File.ReadAllText(relPath);
                    var relConfig = JsonConvert.DeserializeObject<RelationConfig>(relJson);
                    if (relConfig?.Outward != null)
                        foreach (var r in relConfig.Outward)
                            rel.Set(r.Target, r.Type, r.Value, r.Tags);
                    if (relConfig?.Perceived != null)
                        foreach (var p in relConfig.Perceived)
                            rel.PerceiveFrom(p.Source, p.Type, p.Value, p.Confidence, p.Via, 0);
                }
            }

            // Add GoalComponent + load goals
            if (!string.IsNullOrEmpty(config.GoalsPath))
            {
                var goals = entity.Add(new GoalComponent());
                var goalsPath = ResolvePath(config.GoalsPath, rootDir);
                if (File.Exists(goalsPath))
                {
                    var goalsJson = File.ReadAllText(goalsPath);
                    var goalsConfig = JsonConvert.DeserializeObject<GoalConfig>(goalsJson);
                    if (goalsConfig?.Goals != null)
                        foreach (var g in goalsConfig.Goals)
                            goals.AddGoal(g);
                }
                goals.Wire(attrs);
            }

            return entity;
        }

        /// <summary>
        /// Create entity from code, for cases where config is generated at runtime.
        /// </summary>
        public static GameEntity CreateFromCode(string id, Action<GameEntity> configure)
        {
            var entity = new GameEntity(id);
            entity.Add(new Attributes());  // Ensure it has attrs for configure()
            configure(entity);
            return entity;
        }

        private static string ResolvePath(string path, string rootDir)
        {
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(rootDir, path);
        }
    }

    internal class RelationConfig
    {
        [JsonProperty("outward")]
        public List<RelationDef> Outward { get; set; } = new List<RelationDef>();
        [JsonProperty("perceived")]
        public List<PerceivedDef> Perceived { get; set; } = new List<PerceivedDef>();
    }

    internal class RelationDef
    {
        [JsonProperty("target")]
        public string Target { get; set; } = "";
        [JsonProperty("type")]
        public string Type { get; set; } = "";
        [JsonProperty("value")]
        public float Value { get; set; }
        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }

    internal class PerceivedDef
    {
        [JsonProperty("source")]
        public string Source { get; set; } = "";
        [JsonProperty("type")]
        public string Type { get; set; } = "";
        [JsonProperty("value")]
        public float Value { get; set; }
        [JsonProperty("confidence")]
        public float Confidence { get; set; } = 0.5f;
        [JsonProperty("via")]
        public string Via { get; set; } = "initial";
    }

    internal class GoalConfig
    {
        [JsonProperty("goals")]
        public List<Goal> Goals { get; set; } = new List<Goal>();
    }
}
