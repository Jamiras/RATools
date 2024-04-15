using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using RATools.Data;
using RATools.Services;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class LeaderboardViewModel : AssetViewModelBase
    {
        public LeaderboardViewModel(GameViewModel owner)
            : base(owner)
        {
            CopyTitleToClipboardCommand = new DelegateCommand(() =>
            {
                ServiceRepository.Instance.FindService<IClipboardService>().SetData(Title);
            });

            CopyDescriptionToClipboardCommand = new DelegateCommand(() =>
            {
                ServiceRepository.Instance.FindService<IClipboardService>().SetData(Description);
            });
        }

        public override string ViewerType
        {
            get { return "Leaderboard"; }
        }

        public DelegateCommand CopyTitleToClipboardCommand { get; private set; }
        public DelegateCommand CopyDescriptionToClipboardCommand { get; private set; }

        protected override bool AreAssetSpecificPropertiesModified(AssetSourceViewModel left, AssetSourceViewModel right)
        {
            var leftLeaderboard = left.Asset as Leaderboard;
            var rightLeaderboard = right.Asset as Leaderboard;
            if (leftLeaderboard == null || rightLeaderboard == null)
                return false;

            bool isFormatModified = (leftLeaderboard.Format != rightLeaderboard.Format);
            bool isLowerBetterModified = (leftLeaderboard.LowerIsBetter != rightLeaderboard.LowerIsBetter);
            return isFormatModified || isLowerBetterModified;
        }

        internal override IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel)
        {
            var triggers = new List<TriggerViewModel>();
            var leaderboard = assetViewModel.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;

                var notes = _owner != null ? _owner.Notes : new Dictionary<uint, string>();

                triggers.Add(new TriggerViewModel("Start Conditions", leaderboard.Start, numberFormat, notes));
                triggers.Add(new TriggerViewModel("Cancel Conditions", leaderboard.Cancel, numberFormat, notes));
                triggers.Add(new TriggerViewModel("Submit Conditions", leaderboard.Submit, numberFormat, notes));
                triggers.Add(new ValueViewModel("Value", leaderboard.Value, numberFormat, notes));

                if (assetViewModel.Source.StartsWith("Generated"))
                {
                    triggers[0].CopyToClipboardCommand = new DelegateCommand(CopyStartToClipboard);
                    triggers[1].CopyToClipboardCommand = new DelegateCommand(CopyCancelToClipboard);
                    triggers[2].CopyToClipboardCommand = new DelegateCommand(CopySubmitToClipboard);
                    triggers[3].CopyToClipboardCommand = new DelegateCommand(CopyValueToClipboard);
                }
            }

            return triggers.ToArray();
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((Leaderboard)asset, (Leaderboard)localAsset, warning, validateAll);
        }

        private void CopyStartToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Start.Serialize(_owner.SerializationContext));
            }
        }

        private void CopyCancelToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Cancel.Serialize(_owner.SerializationContext));
            }
        }

        private void CopySubmitToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Submit.Serialize(_owner.SerializationContext));
            }
        }

        private void CopyValueToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Value.Serialize(_owner.SerializationContext));
            }
        }
    }
}
