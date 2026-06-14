using System.Collections.Generic;

namespace GameLoop
{
    /// <summary>
    /// Runs a chain of IPrerequisite checks in priority order.
    /// Returns the first unresolved prerequisite as a PrerequisiteBlocker,
    /// or null if all are satisfied.
    ///
    /// Usage:
    ///   var pipeline = new PrerequisitePipeline();
    ///   pipeline.Add(new LocationPrerequisite());
    ///   pipeline.Add(new ItemPrerequisite());       // future
    ///   pipeline.Add(new StaminaPrerequisite());    // future
    ///
    ///   var blocker = pipeline.Run(mapping, ctx);
    ///   if (blocker != null) { /* start resolution action, store pending */ }
    /// </summary>
    public class PrerequisitePipeline
    {
        private readonly List<IPrerequisite> _list = new List<IPrerequisite>();

        /// <summary>Register a prerequisite. Sorted by Priority on each Run.</summary>
        public void Add(IPrerequisite p)
        {
            _list.Add(p);
            _list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// Run all prerequisites in priority order.
        /// Returns the first blocker encountered, or null if all pass.
        /// </summary>
        public PrerequisiteBlocker? Run(ActionMapping mapping, ActionContext ctx)
        {
            foreach (var p in _list)
            {
                var blocker = p.Check(mapping, ctx);
                if (blocker != null)
                {
                    Logger.Debug("Prerequisite", $"Blocked by [{p.Name}]: {mapping.Location ?? "(no location)"}",
                        new { priority = p.Priority });
                    return blocker;
                }
            }
            return null;
        }
    }
}
