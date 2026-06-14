using System.Collections.Generic;

namespace GameLoop
{
    /// <summary>
    /// A prerequisite that must be met before an action can execute.
    /// Implementations are stateless — they check conditions and return
    /// a resolution blocker if the prerequisite is not met.
    /// </summary>
    public interface IPrerequisite
    {
        /// <summary>Lower values are checked first.</summary>
        int Priority { get; }

        /// <summary>Human-readable name for logging.</summary>
        string Name { get; }

        /// <summary>
        /// Check whether the prerequisite is satisfied.
        /// Returns null if met, or a PrerequisiteBlocker describing how to resolve it.
        /// </summary>
        PrerequisiteBlocker? Check(ActionMapping mapping, ActionContext ctx);
    }

    /// <summary>
    /// Returned by IPrerequisite.Check when a prerequisite is not met.
    /// The ActionResolver will execute <see cref="Action"/> as a continuous action,
    /// and re-schedule the original action after it completes.
    /// </summary>
    public class PrerequisiteBlocker
    {
        /// <summary>The continuous action that resolves this prerequisite (e.g., MoveToAction).</summary>
        public IContinuousAction Action { get; set; }

        /// <summary>Parameters for the resolution action.</summary>
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();
    }
}
