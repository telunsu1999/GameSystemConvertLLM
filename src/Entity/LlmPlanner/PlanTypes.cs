using System.Collections.Generic;

namespace GameLoop
{
    public enum PlanType
    {
        Daily,
        Combat,
        Social,
        Emergency
    }

    public class PlanContext
    {
        public PlanType Type { get; set; }
        public int Urgency { get; set; }
        public List<Goal> LongTermGoals { get; set; } = new List<Goal>();
        public List<Goal> ShortTermGoals { get; set; } = new List<Goal>();
        public string TriggerContext { get; set; } = "";
        public string TriggerSignal { get; set; } = "";

        // Time snapshot from TickContext
        public int Tick { get; set; }
        public int Hour { get; set; }
        public int Day { get; set; }
        public string Season { get; set; } = "";
        public string TimeBlock { get; set; } = "";
    }
}
