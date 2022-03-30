using Jamiras.Components;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class AchievementViewModel : AssetViewModelBase
    {
        public AchievementViewModel(GameViewModel owner)
            : base(owner)
        {
        }

        public override string ViewerType
        {
            get { return "Achievement"; }
        }

        public override void Refresh()
        {
            base.Refresh();

            if (String.IsNullOrEmpty(BadgeName))
                BadgeName = "00000";
        }

        protected override bool AreAssetSpecificPropertiesModified(AssetSourceViewModel left, AssetSourceViewModel right)
        {
            IsPointsModified = (left.Points.Value != right.Points.Value);
            return IsPointsModified;
        }

        internal override IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel)
        {
            var achievement = assetViewModel.Asset as Achievement;
            if (achievement != null)
            {
                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
                return new TriggerViewModel[]
                {
                    new TriggerViewModel("", achievement, numberFormat, _owner != null ? _owner.Notes : new TinyDictionary<int, string>())
                };
            }

            return new TriggerViewModel[0];
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((Achievement)asset, (Achievement)localAsset, warning, validateAll);
        }
    }
}
