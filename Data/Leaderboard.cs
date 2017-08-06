using System.Diagnostics;

namespace RATools.Data
{
    [DebuggerDisplay("{Title}")]
    public class Leaderboard
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Start { get; set; }
        public string Cancel { get; set; }
        public string Submit { get; set; }
        public string Value { get; set; }
    }
}
