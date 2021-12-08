using Jamiras.Components;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class LeaderboardViewModel : AssetViewModelBase
    {
        public LeaderboardViewModel(GameViewModel owner, Leaderboard generatedLeaderboard)
            : base(owner, generatedLeaderboard)
        {
        }

        protected override string AssetType
        {
            get { return "Leaderboard"; }
        }

        private void AppendTrigger(List<RequirementGroupViewModel> groups, string header, string trigger)
        {
            var achievementBuilder = new AchievementBuilder();
            achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(trigger));
            var achievement = achievementBuilder.ToAchievement();

            var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
            groups.Add(new RequirementGroupViewModel("Core", achievement.CoreRequirements, numberFormat, _owner.Notes));

            int i = 0;
            foreach (var alt in achievement.AlternateRequirements)
            {
                i++;
                groups.Add(new RequirementGroupViewModel("Alt " + i, alt, numberFormat, _owner.Notes));
            }
        }

        private void AppendValue(List<RequirementGroupViewModel> groups, string header, string trigger)
        {

        }

        internal override IEnumerable<RequirementGroupViewModel> BuildRequirementGroups(AssetSourceViewModel assetViewModel)
        {
            var groups = new List<RequirementGroupViewModel>();
            var leaderboard = assetViewModel.Asset as Leaderboard;
            if (leaderboard != null)
            {
                AppendTrigger(groups, "Start Conditions", leaderboard.Start);
                AppendTrigger(groups, "Cancel Conditions", leaderboard.Cancel);
                AppendTrigger(groups, "Submit Conditions", leaderboard.Submit);

                if (leaderboard.Value.Length > 2 && leaderboard.Value[1] == ':')
                    AppendTrigger(groups, "Value", leaderboard.Value);
                else
                    AppendValue(groups, "Value", leaderboard.Value);
            }

            return groups.ToArray();
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((Achievement)asset, (Achievement)localAsset, warning, validateAll);
        }
    }
}
