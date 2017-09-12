using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Jamiras.Commands;
using RATools.Data;

namespace RATools.ViewModels
{
    public class RichPresenceViewModel : AchievementViewModel
    {
        public RichPresenceViewModel(GameViewModel owner, string richPresence)
            : base(owner)
        {
            Title.Text = "Rich Presence";
            _richPresence = new RequirementViewModel(richPresence, String.Empty);

            _richFile = Path.Combine(owner.RACacheDirectory, owner.GameId + "-Rich.txt");
            if (File.Exists(_richFile))
            {
                _richPresence.Definition.LocalText = File.ReadAllText(_richFile);
                if (String.IsNullOrEmpty(_richPresence.Definition.LocalText))
                    _richPresence.Definition.LocalText = "[Empty]";
            }
        }

        public string Script
        {
            get { return _richPresence.Definition.Text; }
        }

        private readonly RequirementViewModel _richPresence;
        private readonly string _richFile;

        protected override void UpdateLocal()
        {
            File.WriteAllText(_richFile, Script);
        }

        protected override List<RequirementGroupViewModel> BuildRequirementGroups()
        {
            var groups = new List<RequirementGroupViewModel>();
            var group = new RequirementGroupViewModel("Rich Presence", new Requirement[0], _owner.Notes)
            {
                CopyCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, Script))
            };
            ((List<RequirementViewModel>)group.Requirements).Add(_richPresence);
            groups.Add(group);
            return groups;
        }
    }
}
