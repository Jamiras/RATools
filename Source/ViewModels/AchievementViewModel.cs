using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
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
            CopyDefinitionToClipboardCommand = new DelegateCommand(CopyDefinitionToClipboard);
        }

        public override string ViewerType
        {
            get { return "Achievement"; }
        }

        public override void Refresh()
        {
            base.Refresh();

            var generatedAsset = Generated.Asset as Achievement;
            var localAsset = Local.Asset as Achievement;
            var coreAsset = Published.Asset as Achievement;
            if (generatedAsset != null)
                AchievementType = generatedAsset.Type;
            else if (coreAsset != null)
                AchievementType = coreAsset.Type;
            else if (localAsset != null)
                AchievementType = localAsset.Type;

            if (String.IsNullOrEmpty(BadgeName))
                BadgeName = "00000";
        }

        public static readonly ModelProperty AchievementTypeProperty = ModelProperty.Register(typeof(AchievementViewModel), "AchievementType", typeof(int), AchievementType.Standard);
        public AchievementType AchievementType
        {
            get { return (AchievementType)GetValue(AchievementTypeProperty); }
            protected set { SetValue(AchievementTypeProperty, value); }
        }


        public static readonly ModelProperty IsAchievementTypeModifiedProperty = ModelProperty.Register(typeof(AchievementViewModel), "IsAchievementTypeModified", typeof(bool), false);
        public bool IsAchievementTypeModified
        {
            get { return (bool)GetValue(IsAchievementTypeModifiedProperty); }
            protected set { SetValue(IsAchievementTypeModifiedProperty, value); }
        }

        public AchievementType OtherAssetAchievementType
        {
            get { return ((Other.Asset as Achievement)?.Type) ?? AchievementType.None; }
        }

        public string TypeImage
        {
            get 
            {
                switch (this.AchievementType)
                {
                    default:
                    case AchievementType.None: return null;
                    case AchievementType.Missable: return "/RATools;component/Resources/missable.png";
                    case AchievementType.Progression: return "/RATools;component/Resources/progression.png";
                    case AchievementType.WinCondition: return "/RATools;component/Resources/win-condition.png";
                }
            }
        }

        protected override bool AreAssetSpecificPropertiesModified(AssetSourceViewModel left, AssetSourceViewModel right)
        {
            IsPointsModified = (left.Points.Value != right.Points.Value);

            var leftAchievement = left.Asset as Achievement;
            var rightAchievement = right.Asset as Achievement;
            var leftAchievementType = leftAchievement?.Type ?? AchievementType.Standard;
            var rightAchievementType = rightAchievement?.Type ?? AchievementType.Standard;
            IsAchievementTypeModified = leftAchievementType != rightAchievementType;

            return IsPointsModified || IsAchievementTypeModified;
        }

        internal override IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel)
        {
            var achievement = assetViewModel.Asset as Achievement;
            if (achievement != null)
            {
                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
                return new TriggerViewModel[]
                {
                    new TriggerViewModel("", achievement.Trigger, numberFormat, _owner != null ? _owner.Notes : new Dictionary<uint, CodeNote>())
                    {
                        CopyToClipboardCommand = new DelegateCommand(CopyDefinitionToClipboard)
                    }
                };
            }

            return new TriggerViewModel[0];
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((Achievement)asset, (Achievement)localAsset, warning, validateAll);
        }

        public DelegateCommand CopyDefinitionToClipboardCommand { get; private set; }

        private void CopyDefinitionToClipboard()
        {
            var achievement = Generated.Asset as Achievement;
            if (achievement == null)
                achievement = Published.Asset as Achievement;

            if (achievement != null)
            {
                var clipboard = ServiceRepository.Instance.FindService<IClipboardService>();
                clipboard.SetData(achievement.Trigger.Serialize(_owner.SerializationContext));
            }
        }

        public string MeasuredTarget
        {
            get
            {
                var asset = Generated.Asset ?? Local.Asset ?? Published.Asset;
                var achievement = asset as Achievement;
                if (achievement != null)
                {
                    var measuredTarget = GetMeasuredTarget(achievement.CoreRequirements);
                    if (measuredTarget != null)
                        return measuredTarget;

                    foreach (var alt in achievement.AlternateRequirements)
                    {
                        measuredTarget = GetMeasuredTarget(alt);
                        if (measuredTarget != null)
                            return measuredTarget;
                    }
                }

                return null;
            }
        }

        private static string GetMeasuredTarget(IEnumerable<Requirement> requirements)
        {
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.MeasuredPercent)
                    return "100%";

                if (requirement.Type == RequirementType.Measured)
                {
                    if (requirement.HitCount != 0)
                        return requirement.HitCount.ToString();

                    if (requirement.Right.IsMemoryReference)
                        return "?";

                    if (requirement.Right.IsFloat)
                        return requirement.Right.Float.ToString();

                    return requirement.Right.Value.ToString();
                }
            }

            return null;
        }
    }
}
