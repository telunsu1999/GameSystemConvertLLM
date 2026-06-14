using System.Collections.Generic;

namespace GameLoop
{
    /// <summary>
    /// Checks that the NPC is at the required location before executing an action.
    /// If not, returns a MoveToAction blocker to auto-navigate there.
    /// </summary>
    public class LocationPrerequisite : IPrerequisite
    {
        public int Priority => 1;
        public string Name => "Location";

        public PrerequisiteBlocker? Check(ActionMapping mapping, ActionContext ctx)
        {
            // No location requirement → pass
            if (string.IsNullOrEmpty(mapping.Location))
                return null;

            // Already at the right location → pass
            var currentLoc = ctx.Attrs.Get("location")?.Value?.ToString();
            if (currentLoc == mapping.Location)
                return null;

            // No map available → can't navigate, pass through (action will likely fail)
            if (ctx.Map == null)
                return null;

            // Create movement action
            var moveParams = new Dictionary<string, object> { ["target"] = mapping.Location };
            var moveAction = new MoveToAction();
            var moveCtx = new ActionContext
            {
                Attrs = ctx.Attrs,
                Records = ctx.Records,
                Scheduler = ctx.Scheduler,
                Snap = ctx.Snap,
                Params = moveParams,
                Map = ctx.Map
            };

            if (!moveAction.CanExecute(moveCtx))
                return null; // path not reachable → pass (let action handle it)

            return new PrerequisiteBlocker
            {
                Action = moveAction,
                Params = moveParams
            };
        }
    }
}
