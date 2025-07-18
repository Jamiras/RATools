using RATools.Data;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser
{
    public class AchievementScriptContext
    {
        public AchievementScriptContext()
        {
            SerializationContext = new SerializationContext();
        }

        public int GameId { get; set; }
        public List<AchievementSet> Sets { get; set; }
        public Dictionary<Achievement, int> Achievements { get; set; }
        public Dictionary<Leaderboard, int> Leaderboards { get; set; }
        public RichPresenceBuilder RichPresence { get; set; }
        public SerializationContext SerializationContext { get; set; }

        /// <summary>
        /// Gets an <see cref="AchievementSet"/> for the specified achievement set id.
        /// </summary>
        /// <returns>Requested achievement set, <c>null</c> if not found.</returns>
        /// <remarks>If no <see cref="Sets"> are defined, a dummy <see cref="AchievementSet"/> will be returned, populated with the provided id.</remarks>
        public AchievementSet GetSet(int id)
        {
            var set = Sets.FirstOrDefault(s => s.Id == id);
            if (set == null && !Sets.Any())
            {
                set = new AchievementSet
                {
                    Id = id,
                    OwnerSetId = id,
                    OwnerGameId = GameId,
                    Title = "Undefined",
                };
            }

            return set;
        }
    }
}
