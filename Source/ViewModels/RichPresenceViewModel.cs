using Jamiras.Commands;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class RichPresenceViewModel : AssetViewModelBase
    {
        public RichPresenceViewModel(GameViewModel owner)
            : base(owner)
        {
        }

        public override string ViewerType
        {
            get { return "Rich Presence"; }
        }

        public override string ViewerImage
        {
            get { return "/RATools;component/Resources/rich_presence.png"; }
        }


        public DelegateCommand CopyToClipboardCommand { get; private set; }

        internal override IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel)
        {
            var lookups = new List<RequirementGroupViewModel>();

            var richPresence = assetViewModel.Asset as RichPresence;
            if (richPresence != null)
            {
                RequirementGroupViewModel currentGroup = null;

                foreach (var line in richPresence.Script.Split("\r\n"))
                {
                    if (String.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("Format:") || line.StartsWith("Lookup:") || line.StartsWith("Display:"))
                    {
                        currentGroup = new RequirementGroupViewModel(line);
                        lookups.Add(currentGroup);
                    }
                    else if (currentGroup != null)
                    {
                        ((List<RequirementViewModel>)currentGroup.Requirements).Add(
                            new RequirementViewModel(line, String.Empty));
                    }
                }
            }

            return new TriggerViewModel[] { new TriggerViewModel("", lookups.ToArray()) };
        }

        protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
        {
            _owner.UpdateLocal((RichPresence)asset, (RichPresence)localAsset, warning, validateAll);
        }
    }
}
