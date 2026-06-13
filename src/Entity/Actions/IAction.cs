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

    public struct ActionContext
    {
        public Attributes Attrs;
        public RecordModule Records;
        public Scheduler Scheduler;
        public TickSnapshot Snap;
        public Dictionary<string, object> Params;
    }
}
