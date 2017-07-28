using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Data
{
    [DebuggerDisplay("{Title} ({Points})")]
    public class Achievement
    {
        internal Achievement()
        {
            CoreRequirements = new Requirement[0];
            AlternateRequirements = new IEnumerable<Requirement>[0];
        }

        public string Title { get; internal set; }
        public string Description { get; internal set; }
        public int Points { get; internal set; }

        public IEnumerable<Requirement> CoreRequirements { get; internal set; }
        public IEnumerable<IEnumerable<Requirement>> AlternateRequirements { get; internal set; }
    }
}
