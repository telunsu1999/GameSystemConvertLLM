namespace GameLoop
{
    public interface IEntityComponent
    {
        void OnTick(TickContext ctx);
    }

    /// <summary>
    /// Per-tick context passed to each component.
    /// Entity is stored as object to avoid circular project references.
    /// Cast to GameEntity via ctx.GetEntity() extension.
    /// </summary>
    public class TickContext
    {
        public object Entity;
        public int Tick;
        public int Hour;
        public int Day;
        public string Season;
        public string TimeBlock;
        public object Events;
        public bool LlmEnabled;
    }
}
