using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using System.Collections.Generic;
using System.IO;

namespace RATools.ViewModels
{
    public class RichPresenceViewModel : GeneratedItemViewModelBase
    {
        public RichPresenceViewModel(GameViewModel owner, string richPresence)
        {
            Title = "Rich Presence";

            _richPresence = richPresence ?? string.Empty;
            var genLines = _richPresence.Trim().Length > 0 ? _richPresence.Replace("\r\n", "\n").Split('\n') : new string[0];
            string[] localLines = new string[0];
            
            _richFile = Path.Combine(owner.RACacheDirectory, owner.GameId + "-Rich.txt");
            if (File.Exists(_richFile))
            {
                var coreRichPresence = File.ReadAllText(_richFile);
                localLines = coreRichPresence.Replace("\r\n", "\n").Split('\n');
            }

            var lines = new List<RichPresenceLine>();
            int genIndex = 0, localIndex = 0;
            bool isModified = false;

            _hasGenerated = genLines.Length > 0;
            _hasLocal = localLines.Length > 0;

            while (genIndex < genLines.Length && localIndex < localLines.Length)
            {                
                if (genLines[genIndex] == localLines[localIndex])
                {
                    // matching lines, advance both
                    lines.Add(new RichPresenceLine(localLines[localIndex++], genLines[genIndex++]));
                    continue;
                }

                isModified = true;

                if (genLines[genIndex].Length == 0)
                {
                    // blank generated line, advance core
                    lines.Add(new RichPresenceLine(localLines[localIndex++], genLines[genIndex]));
                    continue;
                }

                if (localLines[localIndex].Length == 0)
                {
                    // blank core line, advance generated
                    lines.Add(new RichPresenceLine(localLines[localIndex], genLines[genIndex++]));
                    continue;
                }

                // non-matching lines, scan ahead to find a match
                bool found = false;
                for (int temp = genIndex + 1; temp < genLines.Length; temp++)
                {
                    if (genLines[temp].Length == 0)
                        break;

                    if (genLines[temp] == localLines[localIndex])
                    {
                        while (genIndex < temp)
                            lines.Add(new RichPresenceLine("", genLines[genIndex++]));

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    for (int temp = localIndex + 1; temp < localLines.Length; temp++)
                    {
                        if (localLines[temp].Length == 0)
                            break;

                        if (localLines[temp] == genLines[genIndex])
                        {
                            while (localIndex < temp)
                                lines.Add(new RichPresenceLine(localLines[localIndex++], ""));

                            found = true;
                            break;
                        }
                    }
                }

                // if a match was found, the next iteration will match them. if one wasn't found, advance both.
                if (!found)
                    lines.Add(new RichPresenceLine(localLines[localIndex++], genLines[genIndex++]));
            }

            if (!_hasGenerated)
            {
                foreach (var line in localLines)
                    lines.Add(new RichPresenceLine(line));

                ModificationMessage = null;
                CanUpdate = false;
            }
            else if (isModified || genIndex != genLines.Length || localIndex != localLines.Length)
            {
                while (genIndex < genLines.Length)
                    lines.Add(new RichPresenceLine("", genLines[genIndex++]));
                while (localIndex < localLines.Length)
                    lines.Add(new RichPresenceLine(localLines[localIndex++], ""));

                ModificationMessage = "Local value differs from generated value";
                UpdateLocalCommand = new DelegateCommand(UpdateLocal);
                CanUpdate = true;
            }
            else
            {
                ModificationMessage = null;
                CanUpdate = false;

                lines.Clear();
                foreach (var line in genLines)
                    lines.Add(new RichPresenceLine(line));
            }

            Lines = lines;

            if (_hasGenerated)
            {
                CopyToClipboardCommand = new DelegateCommand(() =>
                {
                    ServiceRepository.Instance.FindService<IClipboardService>().SetData(_richPresence);

                    if (_richPresence.Length > 2500)
                        MessageBoxViewModel.ShowMessage("Rich Presence exceeds maximum length of 2500 characters (" + _richPresence.Length + ")");
                });
            }
        }

        private readonly string _richPresence;
        private readonly string _richFile;
        private bool _hasGenerated;
        private bool _hasLocal;

        public override bool IsGenerated
        {
            get { return _hasGenerated; }
        }

        public class RichPresenceLine
        {
            public RichPresenceLine(string current, string generated)
            {
                Current = current;
                Generated = generated;
                IsModified = current != generated;
            }

            public RichPresenceLine(string line)
            {
                Current = line;
            }

            public string Current { get; private set; }
            public string Generated { get; private set; }
            public bool IsModified { get; private set; }
        }

        public IEnumerable<RichPresenceLine> Lines { get; private set; }

        public DelegateCommand CopyToClipboardCommand { get; private set; }

        private void UpdateLocal()
        {
            File.WriteAllText(_richFile, _richPresence);

            var genLines = _richPresence.Replace("\r\n", "\n").Split('\n');
            var lines = new List<RichPresenceLine>();
            foreach (var line in genLines)
                lines.Add(new RichPresenceLine(line));
            Lines = lines;

            ModificationMessage = null;
            CanUpdate = false;
            OnPropertyChanged(() => Lines);
        }
    }
}
