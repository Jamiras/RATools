using RATools.Data;
using RATools.Parser.Expressions;
using System.Collections.Generic;

namespace RATools.Parser.Internal
{
    internal class AssetExpressionGroup : ExpressionGroup
    {
        public AssetExpressionGroup()
        {
        }

        public Dictionary<Achievement, int> GeneratedAchievements { get; set; }

        public Dictionary<Leaderboard, int> GeneratedLeaderboards { get; set; }

        public RichPresenceBuilder GeneratedRichPresence { get; private set; }

        protected override ExpressionGroup CreateGroup()
        {
            return new AssetExpressionGroup();
        }

        internal void CaptureGeneratedAssets(AchievementScriptContext context)
        {
            if (context.Achievements.Count > 0)
            {
                GeneratedAchievements = context.Achievements;
                context.Achievements = null;
            }
            else
            {
                GeneratedAchievements = null;
            }

            if (context.Leaderboards.Count > 0)
            {
                GeneratedLeaderboards = context.Leaderboards;
                context.Leaderboards = null;
            }
            else
            {
                GeneratedLeaderboards = null;
            }

            if (!context.RichPresence.IsEmpty)
            {
                GeneratedRichPresence = context.RichPresence;
                context.RichPresence = null;
            }
        }

        internal override void AdjustSourceLines(int adjustment)
        {
            if (GeneratedAchievements != null)
            {
                var newAchievements = new Dictionary<Achievement, int>(GeneratedAchievements.Count);
                foreach (var kvp in GeneratedAchievements)
                    newAchievements[kvp.Key] = kvp.Value + adjustment;
                GeneratedAchievements = newAchievements;
            }

            if (GeneratedLeaderboards != null)
            {
                var newLeaderboards = new Dictionary<Leaderboard, int>(GeneratedLeaderboards.Count);
                foreach (var kvp in GeneratedLeaderboards)
                    newLeaderboards[kvp.Key] = kvp.Value + adjustment;
                GeneratedLeaderboards = newLeaderboards;
            }

            if (GeneratedRichPresence != null)
                GeneratedRichPresence.Line += adjustment;
        }
    }

    internal class AssetExpressionGroupCollection : ExpressionGroupCollection
    {
        public AssetExpressionGroupCollection()
        {
        }

        protected override ExpressionGroup CreateGroup()
        {
            return new AssetExpressionGroup();
        }
    }
}
