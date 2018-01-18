using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.DataModels.Metadata;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Converters;
using Jamiras.ViewModels.Fields;
using Jamiras.ViewModels.Grid;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RATools.ViewModels
{
    public class NewScriptDialogViewModel : DialogViewModelBase
    {
        public NewScriptDialogViewModel()
        {
            DialogTitle = "New Script";
            CanClose = true;

            SearchCommand = new DelegateCommand(Search);
            CheckAllCommand = new DelegateCommand(CheckAll);
            UncheckAllCommand = new DelegateCommand(UncheckAll);
            CheckWithTicketsCommand = new DelegateCommand(CheckWithTickets);

            GameId = new IntegerFieldViewModel("Game _ID", 1, 999999);

            _achievements = new ObservableCollection<DumpAchievementItem>();
            _memoryItems = new List<MemoryItem>();
            _ticketNotes = new TinyDictionary<int, string>();

            MemoryAddresses = new GridViewModel();
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Size", MemoryItem.SizeProperty, new DelegateConverter(a => Field.GetSizeFunction((FieldSize)a), null)) { Width = 48 });
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Address", MemoryItem.AddressProperty, new DelegateConverter(a => String.Format("0x{0:X6}", a), null)) { Width = 56 });
            MemoryAddresses.Columns.Add(new TextColumnDefinition("Function Name", MemoryItem.FunctionNameProperty, new StringFieldMetadata("Function Name", 40, StringFieldAttributes.Required)) { Width = 120 });
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Notes", MemoryItem.NotesProperty));
        }

        public IntegerFieldViewModel GameId { get; private set; }

        public static readonly ModelProperty IsGameLoadedProperty = ModelProperty.Register(typeof(NewScriptDialogViewModel), "IsGameLoaded", typeof(bool), false);
        public bool IsGameLoaded
        {
            get { return (bool)GetValue(IsGameLoadedProperty); }
            private set { SetValue(IsGameLoadedProperty, value); }
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            int gameId = GameId.Value.GetValueOrDefault();
            foreach (var directory in ServiceRepository.Instance.FindService<ISettings>().DataDirectories)
            {
                var notesFile = Path.Combine(directory, gameId + "-Notes2.txt");
                if (File.Exists(notesFile))
                {
                    LoadGame(gameId, directory);
                    return;
                }
            }

            MessageBoxViewModel.ShowMessage("Could not locate notes file for game " + gameId);
            return;
        }

        private void LoadGame(int gameId, string raCacheDirectory)
        {
            _game = new GameViewModel(GameId.Value.GetValueOrDefault(), "", raCacheDirectory);
            DialogTitle = "New Script - " + _game.Title;

            _achievements.Clear();
            _ticketNotes.Clear();
            _memoryItems.Clear();
            MemoryAddresses.Rows.Clear();

            var unofficialAchievements = new List<DumpAchievementItem>();
            foreach (var achievement in _game.Achievements.OfType<GeneratedAchievementViewModel>())
            {
                AchievementViewModel source = achievement.Core;
                if (source.Achievement == null)
                {
                    source = achievement.Unofficial;
                    if (source.Achievement == null)
                        continue;
                }

                var dumpAchievement = new DumpAchievementItem(achievement.Id, source.Title.Text);
                if (achievement.Core.Achievement == null)
                {
                    dumpAchievement.IsUnofficial = true;
                    unofficialAchievements.Add(dumpAchievement);
                }
                else
                {
                    _achievements.Add(dumpAchievement);
                }

                foreach (var group in source.RequirementGroups)
                {
                    foreach (var requirement in group.Requirements)
                    {
                        if (requirement.Requirement == null)
                            continue;

                        if (requirement.Requirement.Left != null && (requirement.Requirement.Left.Type == FieldType.MemoryAddress || requirement.Requirement.Left.Type == FieldType.PreviousValue))
                        {
                            var memoryItem = AddMemoryAddress(requirement.Requirement.Left);
                            if (memoryItem != null && !dumpAchievement.MemoryAddresses.Contains(memoryItem))
                                dumpAchievement.MemoryAddresses.Add(memoryItem);
                        }

                        if (requirement.Requirement.Right != null && (requirement.Requirement.Right.Type == FieldType.MemoryAddress || requirement.Requirement.Right.Type == FieldType.PreviousValue))
                        {
                            var memoryItem = AddMemoryAddress(requirement.Requirement.Right);
                            if (memoryItem != null && !dumpAchievement.MemoryAddresses.Contains(memoryItem))
                                dumpAchievement.MemoryAddresses.Add(memoryItem);
                        }
                    }
                }

                dumpAchievement.IsSelected = true;
                dumpAchievement.PropertyChanged += DumpAchievement_PropertyChanged;
            }

            foreach (var unofficialAchievement in unofficialAchievements)
                _achievements.Add(unofficialAchievement);

            foreach (var kvp in _game.Notes)
            {
                FieldSize size = FieldSize.Byte;
                Token token = new Token(kvp.Value, 0, kvp.Value.Length);
                if (token.Contains("16-bit", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("16 bit", StringComparison.OrdinalIgnoreCase))
                {
                    size = FieldSize.Word;
                }
                else if (token.Contains("32-bit", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("32 bit", StringComparison.OrdinalIgnoreCase))
                {
                    size = FieldSize.DWord;
                }

                AddMemoryAddress(new Field { Size = size, Type = FieldType.MemoryAddress, Value = (uint)kvp.Key });
            }


            UpdateMemoryGrid();

            IsGameLoaded = true;
            GameId.IsEnabled = false;

            if (_achievements.Count > 0)
                ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(MergeOpenTickets);
        }

        private void MergeOpenTickets()
        {
            var openTickets = new List<int>();

            var tickets = OpenTicketsViewModel.GetGameTickets(_game.GameId);
            foreach (var kvp in tickets)
            {
                var achievement = _achievements.FirstOrDefault(a => a.Id == kvp.Key);
                if (achievement != null)
                {
                    openTickets.AddRange(kvp.Value.OpenTickets);
                    achievement.OpenTickets.AddRange(kvp.Value.OpenTickets);
                    achievement.RaiseOpenTicketCountChanged();
                }
            }

            foreach (var ticket in openTickets)
            {
                var ticketPage = RAWebCache.Instance.GetTicketPage(ticket);
                var tokenizer = Tokenizer.CreateTokenizer(ticketPage);
                tokenizer.ReadTo("<td>Notes: </td>");
                tokenizer.ReadTo("<code>");
                tokenizer.Advance(6);

                var notes = tokenizer.ReadTo("</code>").ToString();
                _ticketNotes[ticket] = notes.ToString();
            }
        }

        private MemoryItem AddMemoryAddress(Field field)
        {
            int index = 0;
            while (index < _memoryItems.Count)
            {
                if (_memoryItems[index].Address > field.Value)
                    break;
                if (_memoryItems[index].Address == field.Value)
                {
                    if (_memoryItems[index].Size > field.Size)
                        break;
                    if (_memoryItems[index].Size == field.Size)
                        return _memoryItems[index];
                }

                index++;
            }

            string notes;
            if (!_game.Notes.TryGetValue((int)field.Value, out notes))
                return null;

            var item = new MemoryItem(field.Value, field.Size, notes.Replace("\r\n", " ").Replace('\n', ' '));
            _memoryItems.Insert(index, item);
            return item;
        }

        private GameViewModel _game;
        private readonly TinyDictionary<int, string> _ticketNotes;
        
        public class DumpAchievementItem : LookupItem
        {
            public DumpAchievementItem(int id, string label)
                : base(id, label)
            {
                OpenTickets = new List<int>();
                MemoryAddresses = new List<MemoryItem>();
            }

            public bool IsUnofficial { get; set; }

            public int OpenTicketCount
            {
                get { return OpenTickets.Count; }
            }
            internal List<int> OpenTickets { get; private set; }

            internal void RaiseOpenTicketCountChanged()
            {
                OnPropertyChanged(() => OpenTicketCount);
            }

            internal List<MemoryItem> MemoryAddresses { get; private set; }
        }

        public IEnumerable<DumpAchievementItem> Achievements
        {
            get { return _achievements; }
        }
        private readonly ObservableCollection<DumpAchievementItem> _achievements;
        
        private void DumpAchievement_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected")
                UpdateMemoryGrid();
        }

        public CommandBase CheckAllCommand { get; private set; }
        private void CheckAll()
        {
            foreach (var achievment in _achievements)
                achievment.IsSelected = true;
        }

        public CommandBase UncheckAllCommand { get; private set; }
        private void UncheckAll()
        {
            foreach (var achievment in _achievements)
                achievment.IsSelected = false;
        }

        public CommandBase CheckWithTicketsCommand { get; private set; }
        private void CheckWithTickets()
        {
            foreach (var achievment in _achievements)
                achievment.IsSelected = (achievment.OpenTicketCount > 0);
        }

        public static readonly ModelProperty MemoryAddressesLabelProperty = ModelProperty.Register(typeof(NewScriptDialogViewModel), "MemoryAddressesLabel", typeof(string), "Referenced memory addresses");
        public string MemoryAddressesLabel
        {
            get { return (string)GetValue(MemoryAddressesLabelProperty); }
            private set { SetValue(MemoryAddressesLabelProperty, value); }
        }

        public GridViewModel MemoryAddresses { get; private set; }

        private void UpdateMemoryGrid()
        {
            var visibleAddresses = new List<MemoryItem>();
            foreach (var achievement in _achievements)
            {
                if (!achievement.IsSelected)
                    continue;

                foreach (var address in achievement.MemoryAddresses)
                {
                    if (!visibleAddresses.Contains(address))
                        visibleAddresses.Add(address);
                }
            }

            var rowsToRemove = new List<MemoryItem>();
            foreach (var row in MemoryAddresses.Rows)
            {
                var memoryItem = (MemoryItem)row.Model;
                if (!visibleAddresses.Remove(memoryItem))
                    rowsToRemove.Add(memoryItem);
            }
            foreach (var memoryItem in rowsToRemove)
                MemoryAddresses.RemoveRow(memoryItem);

            if (visibleAddresses.Count > 0 || MemoryAddresses.Rows.Count > 0)
            {
                MemoryAddressesLabel = (string)MemoryAddressesLabelProperty.DefaultValue;
            }
            else
            {
                MemoryAddressesLabel = "All known memory addresses";
                foreach (var memoryItem in _memoryItems)
                    visibleAddresses.Add(memoryItem);
            }

            visibleAddresses.Sort((l,r) =>
            {
                int diff = (int)l.Address - (int)r.Address;
                if (diff == 0)
                    diff = (int)l.Size - (int)r.Size;
                return diff;
            });

            var memIndex = 0;
            for (int addrIndex = 0; addrIndex < visibleAddresses.Count; addrIndex++)
            {
                var memoryItem = visibleAddresses[addrIndex];
                while (memIndex < MemoryAddresses.Rows.Count)
                {
                    var rowItem = (MemoryItem)MemoryAddresses.Rows[memIndex].Model;
                    if (rowItem.Address < memoryItem.Address || rowItem.Size < memoryItem.Size)
                    {
                        memIndex++;
                        continue;
                    }

                    break;
                }

                MemoryAddresses.InsertRow(memIndex, memoryItem);
            }
        }

        [DebuggerDisplay("{Size} {Address}")]
        public class MemoryItem : ViewModelBase
        {
            public MemoryItem(uint address, FieldSize size, string notes)
            {
                Address = address;
                Size = size;
                Notes = notes;
            }

            public static readonly ModelProperty AddressProperty = ModelProperty.Register(typeof(MemoryItem), "Address", typeof(uint), (uint)0);

            public uint Address
            {
                get { return (uint)GetValue(AddressProperty); }
                private set { SetValue(AddressProperty, value); }
            }

            public static readonly ModelProperty SizeProperty = ModelProperty.Register(typeof(MemoryItem), "Size", typeof(FieldSize), FieldSize.Byte);
            public FieldSize Size
            {
                get { return (FieldSize)GetValue(SizeProperty); }
                private set { SetValue(SizeProperty, value); }
            }

            public static readonly ModelProperty FunctionNameProperty = ModelProperty.Register(typeof(MemoryItem), "FunctionName", typeof(string), String.Empty);

            public string FunctionName
            {
                get { return (string)GetValue(FunctionNameProperty); }
                set { SetValue(FunctionNameProperty, value); }
            }

            public static readonly ModelProperty NotesProperty = ModelProperty.Register(typeof(MemoryItem), "Notes", typeof(string), String.Empty);

            public string Notes
            {
                get { return (string)GetValue(NotesProperty); }
                private set { SetValue(NotesProperty, value); }
            }
        }

        private readonly List<MemoryItem> _memoryItems;

        protected override void ExecuteOkCommand()
        {
            var errors = Validate();
            if (!String.IsNullOrEmpty(errors))
            {
                MessageBoxViewModel.ShowMessage(errors);
                return;
            }

            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Create Script File";
            vm.Filters["Script file"] = "*.txt";

            var cleansed = _game.Title;
            foreach (var c in Path.GetInvalidFileNameChars())
                cleansed = cleansed.Replace(c.ToString(), "");
            if (String.IsNullOrEmpty(cleansed))
                cleansed = _game.GameId.ToString();
            vm.FileNames = new[] { cleansed + ".txt" };

            if (vm.ShowSaveFileDialog() != DialogResult.Ok)
                return;

            Dump(vm.FileNames[0]);
        }

        private void Dump(string filename)
        {
            MemoryAddresses.Commit();

            var logger = ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools");
            logger.WriteVerbose("Dumping to file " + filename);

            using (var stream = File.CreateText(filename))
            {
                stream.Write("// ");
                stream.WriteLine(_game.Title);
                stream.Write("// #ID = ");
                stream.WriteLine(String.Format("{0}", _game.GameId));
                stream.WriteLine();

                foreach (var memoryItem in _memoryItems)
                {
                    if (!String.IsNullOrEmpty(memoryItem.FunctionName))
                    {
                        string notes;
                        if (_game.Notes.TryGetValue((int)memoryItem.Address, out notes))
                        {
                            notes = notes.Trim();
                            if (notes.Length > 0)
                            {
                                stream.WriteLine();
                                foreach (var line in notes.Split('\n'))
                                {
                                    stream.Write("// ");
                                    stream.WriteLine(line.Trim());
                                }
                            }
                        }

                        stream.Write("function ");
                        stream.Write(memoryItem.FunctionName);
                        stream.Write("() => ");
                        var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                        stream.WriteLine(memoryReference);
                    }
                }

                stream.WriteLine();

                foreach (var dumpAchievement in _achievements)
                {
                    if (!dumpAchievement.IsSelected)
                        continue;

                    var achievement = _game.Achievements.FirstOrDefault(a => a.Id == dumpAchievement.Id) as GeneratedAchievementViewModel;
                    if (achievement == null)
                        continue;

                    foreach (var ticket in dumpAchievement.OpenTickets)
                    {
                        string notes;
                        if (_ticketNotes.TryGetValue(ticket, out notes))
                        {
                            stream.Write("// ");
                            stream.Write(ticket);
                            stream.Write(": ");
                            stream.WriteLine(notes.Replace("\n", "\n//        "));
                        }
                    }

                    stream.WriteLine("achievement(");

                    stream.Write("    title = \"");
                    stream.Write(achievement.Core.Title.Text);
                    stream.Write("\", description = \"");
                    stream.Write(achievement.Core.Description.Text);
                    stream.Write("\", points = ");
                    stream.Write(achievement.Core.Points.Value);
                    stream.WriteLine(",");

                    stream.Write("    id = ");
                    stream.Write(achievement.Core.Achievement.Id);
                    stream.Write(", badge = \"");
                    stream.Write(achievement.Core.Achievement.BadgeName);
                    stream.Write("\", published = \"");
                    stream.Write(achievement.Core.Achievement.Published);
                    stream.Write("\", modified = \"");
                    stream.Write(achievement.Core.Achievement.LastModified);
                    stream.WriteLine("\",");

                    var groupEnumerator = achievement.Core.RequirementGroups.GetEnumerator();
                    groupEnumerator.MoveNext();
                    stream.Write("    trigger = ");
                    DumpPublishedRequirements(stream, dumpAchievement, groupEnumerator.Current);
                    bool first = true;
                    while (groupEnumerator.MoveNext())
                    {
                        if (first)
                        {
                            stream.WriteLine(" &&");
                            stream.Write("              ((");
                            first = false;
                        }
                        else
                        {
                            stream.WriteLine(" ||");
                            stream.Write("               (");
                        }

                        DumpPublishedRequirements(stream, dumpAchievement, groupEnumerator.Current);
                        stream.Write(")");
                    }
                    if (!first)
                        stream.Write(')');

                    stream.WriteLine();

                    stream.WriteLine(")");
                    stream.WriteLine();
                }
            }

            var vm = new MessageBoxViewModel(Path.GetFileName(filename) + " created.");
            vm.DialogTitle = DialogTitle;
            vm.ShowDialog();
        }

        private void DumpPublishedRequirements(StreamWriter stream, DumpAchievementItem dumpAchievement, RequirementGroupViewModel requirementGroupViewModel)
        {
            bool needsAmpersand = false;

            var requirementEnumerator = requirementGroupViewModel.Requirements.GetEnumerator();
            while (requirementEnumerator.MoveNext())
            {
                if (!String.IsNullOrEmpty(requirementEnumerator.Current.Definition))
                {
                    if (needsAmpersand)
                        stream.Write(" && ");
                    else
                        needsAmpersand = true;

                    var definition = requirementEnumerator.Current.Definition;
                    foreach (var memoryItem in dumpAchievement.MemoryAddresses.Where(m => !String.IsNullOrEmpty(m.FunctionName)))
                    {
                        var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                        var functionCall = memoryItem.FunctionName + "()";
                        definition = definition.Replace(memoryReference, functionCall);
                    }

                    stream.Write(definition);

                    switch (requirementEnumerator.Current.Requirement.Type)
                    {
                        case RequirementType.AddSource:
                        case RequirementType.SubSource:
                        case RequirementType.AddHits:
                            needsAmpersand = false;
                            break;
                    }
                }
            }
        }
    }
}
