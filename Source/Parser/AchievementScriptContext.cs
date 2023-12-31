using RATools.Data;
using System.Collections.Generic;

namespace RATools.Parser
{
    public class AchievementScriptContext
    {
        public ICollection<Achievement> Achievements { get; set; }
        public ICollection<Leaderboard> Leaderboards { get; set; }
        public RichPresenceBuilder RichPresence { get; set; }
    }
}
