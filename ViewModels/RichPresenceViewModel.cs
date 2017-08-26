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
            _richPresence = richPresence;
            Title = "Rich Presence";

            _richFile = Path.Combine(owner.RACacheDirectory, owner.GameId + "-Rich.txt");
            if (File.Exists(_richFile))
            {
                var richLocal = File.ReadAllText(_richFile);
                if (String.IsNullOrEmpty(_richPresence))
                    _richPresence = richLocal;
                else if (_richPresence != richLocal)
                    IsDifferentThanLocal = true;
            }
        }

        public string Script
        {
            get { return _richPresence; }
        }

        private readonly string _richPresence;
        private readonly string _richFile;

        protected override void UpdateLocal()
        {
            File.WriteAllText(_richFile, _richPresence);
            IsDifferentThanLocal = false;
        }

        protected override List<RequirementGroupViewModel> BuildRequirementGroups()
        {
            var groups = new List<RequirementGroupViewModel>();
            var group = new RequirementGroupViewModel("Rich Presence", new Requirement[0], _owner.Notes)
            {
                CopyCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _richPresence))
            };
            ((List<RequirementViewModel>)group.Requirements).Add(new RequirementViewModel(_richPresence, String.Empty));
            groups.Add(group);
            return groups;
        }
    }
}
