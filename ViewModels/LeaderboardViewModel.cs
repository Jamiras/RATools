using Jamiras.Components;
using RATools.Data;
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

        public override string ViewerType
        {
            get { return "Leaderboard"; }
        }

        internal override IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel)
        {
            var triggers = new List<TriggerViewModel>();
            var leaderboard = assetViewModel.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;

                triggers.Add(new TriggerViewModel("Start Conditions", leaderboard.Start, numberFormat, _owner.Notes));
                triggers.Add(new TriggerViewModel("Cancel Conditions", leaderboard.Cancel, numberFormat, _owner.Notes));
                triggers.Add(new TriggerViewModel("Submit Conditions", leaderboard.Submit, numberFormat, _owner.Notes));
                triggers.Add(new TriggerViewModel("Value", leaderboard.Value, numberFormat, _owner.Notes));
            }

            return triggers.ToArray();
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((Leaderboard)asset, (Leaderboard)localAsset, warning, validateAll);
        }
    }
}
