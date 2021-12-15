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
                clipboard.SetData(leaderboard.Start);
            }
        }

        private void CopyCancelToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Cancel);
            }
        }

        private void CopySubmitToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Submit);
            }
        }

        private void CopyValueToClipboard()
        {
            var leaderboard = Generated.Asset as Leaderboard;
            if (leaderboard != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(leaderboard.Value);
            }
        }
    }
}
