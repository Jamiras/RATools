using Jamiras.Components;
using RATools.Data;
using RATools.Services;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class AchievementViewModel : AssetViewModelBase
    {
        public AchievementViewModel(GameViewModel owner, Achievement generatedAchievement)
            : base(owner, generatedAchievement)
        {
        }

        protected override string AssetType
        {
            get { return "Achievement"; }
        }

        internal override IEnumerable<RequirementGroupViewModel> BuildRequirementGroups(AssetSourceViewModel assetViewModel)
        {
            var groups = new List<RequirementGroupViewModel>();

            var achievement = assetViewModel.Asset as Achievement;
            if (achievement != null)
            {
                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
                groups.Add(new RequirementGroupViewModel("Core", achievement.CoreRequirements, numberFormat, _owner.Notes));


                int i = 0;
                foreach (var alt in achievement.AlternateRequirements)
                {
                    i++;
                    groups.Add(new RequirementGroupViewModel("Alt " + i, alt, numberFormat, _owner.Notes));
                }
            }

            return groups.ToArray();
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((Achievement)asset, (Achievement)localAsset, warning, validateAll);
        }
    }
}
