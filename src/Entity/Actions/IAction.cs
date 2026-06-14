using System.Collections.Generic;

namespace GameLoop
{
    public interface IAction
    {
        string ActionType { get; }
        bool Interruptible { get; }

        bool CanExecute(ActionContext ctx);
        IEnumerable<IAction> Execute(ActionContext ctx);
        void OnInterrupt(ActionContext ctx);
    }

    /// <summary>
    /// Optional extension for actions that persist across multiple ticks.
    /// Implement this (in addition to IAction) when the action needs to
    /// modify data on every tick while it is executing — e.g. movement,
    /// spell channelling, construction progress.
    ///
    /// ActionResolver calls OnTick each tick while the action is current.
    /// When IsCompleted becomes true, the resolver clears the current action,
    /// freeing the entity back to idle.
    /// </summary>
    public interface IContinuousAction : IAction
    {
        /// <summary>Called each tick while this action is the current action.</summary>
        /// <returns>Child actions to schedule (e.g. trigger checks on waypoint arrival).</returns>
        IEnumerable<IAction> OnTick(ActionContext ctx);

        /// <summary>Whether the continuous action has finished.</summary>
        bool IsCompleted { get; }
    }

    public struct ActionContext
    {
        public Attributes Attrs;
        public RecordModule Records;
        public Scheduler Scheduler;
        public TickSnapshot Snap;
        public Dictionary<string, object> Params;

        /// <summary>
        /// Reference to the global MapSystem, if one is loaded.
        /// Set by ActionResolver before dispatching. May be null when no map is loaded.
        /// </summary>
        public MapSystem Map;
    }
}
