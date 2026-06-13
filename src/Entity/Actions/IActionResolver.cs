using System.Collections.Generic;

namespace GameLoop
{
    public interface IActionResolver
    {
        CurrentAction Current { get; }
        void LoadMapping(string jsonPath);
        void Register(IAction action);
        ExecuteResult Execute(ActionItem item, Attributes attrs, Scheduler scheduler, TickSnapshot snap, RecordModule records = null);
        void Interrupt(Attributes attrs, Scheduler scheduler, TickSnapshot snap);
        List<ActionSchema> GetAvailableActions();
    }

    public class ActionSchema
    {
        public string ActionType;
        public string Description;
        public string Effect;
        public List<ParamSchema> Params;
    }
}
