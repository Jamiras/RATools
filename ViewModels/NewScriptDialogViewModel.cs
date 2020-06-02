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
using RATools.Parser;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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
            _macros = new List<RichPresenceMacro>();

            CodeNoteFilters = new[]
            {
                new CodeNoteFilterLookupItem(CodeNoteFilter.All, "All"),
                new CodeNoteFilterLookupItem(CodeNoteFilter.ForSelectedAchievements, "For Selected Achievements"),
            };

            NoteDumps = new[]
            {
                new NoteDumpLookupItem(NoteDump.None, "None"),
                new NoteDumpLookupItem(NoteDump.All, "All"),
                new NoteDumpLookupItem(NoteDump.OnlyForDefinedMethods, "Only for Functions"),
            };

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
            foreach (var directory in ServiceRepository.Instance.FindService<ISettings>().EmulatorDirectories)
            {
                var dataDirectory = Path.Combine(directory, "RACache", "Data");

                var notesFile = Path.Combine(dataDirectory, gameId + "-Notes.json");
                if (!File.Exists(notesFile))
                    notesFile = Path.Combine(dataDirectory, gameId + "-Notes2.txt");

                if (File.Exists(notesFile))
                {
                    LoadGame(gameId, dataDirectory);

                    var richPresenceFile = Path.Combine(dataDirectory, gameId + "-Rich.txt");
                    if (File.Exists(richPresenceFile))
                        LoadRichPresence(richPresenceFile);

                    return;
                }
            }

            TaskDialogViewModel.ShowWarningMessage("Could not locate code notes for game " + gameId,
                "The game does not appear to have been recently loaded in any of the emulators specified in the Settings dialog.");
            return;
        }

        private void LoadGame(int gameId, string raCacheDirectory)
        {
            _game = new GameViewModel(GameId.Value.GetValueOrDefault(), "", raCacheDirectory);
            _game.PopulateEditorList(null);
            DialogTitle = "New Script - " + _game.Title;

            _achievements.Clear();
            _ticketNotes.Clear();
            _memoryItems.Clear();
            MemoryAddresses.Rows.Clear();

            var unofficialAchievements = new List<DumpAchievementItem>();
            foreach (var achievement in _game.Editors.OfType<GeneratedAchievementViewModel>())
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

                        if (requirement.Requirement.Left != null && requirement.Requirement.Left.IsMemoryReference)
                        {
                            var memoryItem = AddMemoryAddress(requirement.Requirement.Left);
                            if (memoryItem != null && !dumpAchievement.MemoryAddresses.Contains(memoryItem))
                                dumpAchievement.MemoryAddresses.Add(memoryItem);
                        }

                        if (requirement.Requirement.Right != null && requirement.Requirement.Right.IsMemoryReference)
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
                else if (token.Contains("24-bit", StringComparison.OrdinalIgnoreCase) ||
                    token.Contains("24 bit", StringComparison.OrdinalIgnoreCase))
                {
                    size = FieldSize.TByte;
                }

                AddMemoryAddress(new Field { Size = size, Type = FieldType.MemoryAddress, Value = (uint)kvp.Key });
            }

            if (_achievements.Count == 0)
                SelectedCodeNotesFilter = CodeNoteFilter.All;

            UpdateMemoryGrid();

            IsGameLoaded = true;
            GameId.IsEnabled = false;

            if (_achievements.Count > 0)
                ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(MergeOpenTickets);
        }

        private class RichPresenceMacro
        {
            public string Name;
            public ValueFormat FormatType;
            public Dictionary<string, string> LookupEntries;
            public List<string> DisplayLines;
        }
        private List<RichPresenceMacro> _macros;

        private void LoadRichPresence(string richPresenceFile)
        {
            RichPresenceMacro currentMacro = null;
            RichPresenceMacro displayMacro = null;

            using (var file = File.OpenText(richPresenceFile))
            {
                do
                {
                    var line = file.ReadLine();
                    if (line == null)
                        break;

                    var index = line.IndexOf("//");
                    if (index != -1)
                        line = line.Substring(0, index).TrimEnd();

                    if (line.Length == 0)
                        continue;

                    if (line.StartsWith("Format:"))
                    {
                        currentMacro = new RichPresenceMacro { Name = line.Substring(7) };
                        _macros.Add(currentMacro);
                    }
                    else if (line.StartsWith("FormatType="))
                    {
                        currentMacro.FormatType = Leaderboard.ParseFormat(line.Substring(11));
                    }
                    else if (line.StartsWith("Lookup:"))
                    {
                        currentMacro = new RichPresenceMacro { Name = line.Substring(7), LookupEntries = new Dictionary<string, string>() };
                        _macros.Add(currentMacro);
                    }
                    else if (line.StartsWith("Display:"))
                    {
                        currentMacro = displayMacro = new RichPresenceMacro { Name = "Display", DisplayLines = new List<string>() };
                        _macros.Add(currentMacro);
                    }
                    else
                    {
                        if (currentMacro.DisplayLines != null)
                        {
                            currentMacro.DisplayLines.Add(line);
                        }
                        else if (currentMacro.LookupEntries != null)
                        {
                            index = line.IndexOf('=');
                            if (index > 0)
                                currentMacro.LookupEntries[line.Substring(0, index)] = line.Substring(index + 1);
                        }
                    }

                } while (true);
            }

            foreach (var macro in _macros)
            {
                if (macro.LookupEntries != null)
                {
                    var dumpLookup = new DumpAchievementItem(0, macro.Name) { IsLookup = true };
                    dumpLookup.PropertyChanged += DumpAchievement_PropertyChanged;
                    _achievements.Add(dumpLookup);
                }
            }

            if (displayMacro != null)
            {
                var dumpRichPresence = new DumpAchievementItem(0, "Rich Presence Script") { IsRichPresence = true };

                for (int i = 0; i < displayMacro.DisplayLines.Count; ++i)
                {
                    var line = displayMacro.DisplayLines[i];
                    if (line[0] == '?')
                    {
                        var index = line.IndexOf('?', 1);
                        if (index != -1)
                        {
                            var trigger = line.Substring(1, index - 1);
                            var achievement = new AchievementBuilder();
                            achievement.ParseRequirements(Tokenizer.CreateTokenizer(trigger));

                            foreach (var requirement in achievement.CoreRequirements)
                                AddMemoryReferences(dumpRichPresence, requirement);

                            foreach (var alt in achievement.AlternateRequirements)
                                foreach (var requirement in alt)
                                    AddMemoryReferences(dumpRichPresence, requirement);
                        }

                        AddMacroMemoryReferences(dumpRichPresence, line.Substring(index + 1));
                    }
                    else
                    {
                        AddMacroMemoryReferences(dumpRichPresence, line);

                        if (i < displayMacro.DisplayLines.Count)
                            displayMacro.DisplayLines.RemoveRange(i + 1, displayMacro.DisplayLines.Count - i - 1);
                        break;
                    }
                }

                dumpRichPresence.PropertyChanged += DumpAchievement_PropertyChanged;
                _achievements.Add(dumpRichPresence);
            }
        }

        private void AddMacroMemoryReferences(DumpAchievementItem displayRichPresence, string displayString)
        {
            var index = 0;
            do
            {
                index = displayString.IndexOf('@', index);
                if (index == -1)
                    return;
                var index2 = displayString.IndexOf('(', index);
                if (index2 == -1)
                    return;
                var index3 = displayString.IndexOf(')', index2);
                if (index3 == -1)
                    return;

                var name = displayString.Substring(index + 1, index2 - index - 1);
                var parameter = displayString.Substring(index2 + 1, index3 - index2 - 1);

                var macro = _achievements.FirstOrDefault(a => a.IsLookup && a.Label == name);
                if (macro == null)
                    macro = displayRichPresence;

                if (parameter[1] == ':')
                {
                    var achievement = new AchievementBuilder();
                    achievement.ParseRequirements(Tokenizer.CreateTokenizer(parameter));

                    foreach (var requirement in achievement.CoreRequirements)
                        AddMemoryReferences(macro, requirement);

                    foreach (var alt in achievement.AlternateRequirements)
                        foreach (var requirement in alt)
                            AddMemoryReferences(macro, requirement);
                }
                else
                {
                    foreach (var part in parameter.Split('_'))
                    {
                        foreach (var operand in part.Split('*'))
                        {
                            var field = Field.Deserialize(Tokenizer.CreateTokenizer(operand));
                            if (field.IsMemoryReference)
                            {
                                var memoryItem = AddMemoryAddress(field);
                                if (memoryItem != null && !macro.MemoryAddresses.Contains(memoryItem))
                                    macro.MemoryAddresses.Add(memoryItem);
                            }
                        }
                    }
                }

                index = index2 + 1;
            } while (true);
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

        private void AddMemoryReferences(DumpAchievementItem dumpAchievement, Requirement requirement)
        {
            if (requirement.Left != null && requirement.Left.IsMemoryReference)
            {
                var memoryItem = AddMemoryAddress(requirement.Left);
                if (memoryItem != null && !dumpAchievement.MemoryAddresses.Contains(memoryItem))
                    dumpAchievement.MemoryAddresses.Add(memoryItem);
            }

            if (requirement.Right != null && requirement.Right.IsMemoryReference)
            {
                var memoryItem = AddMemoryAddress(requirement.Right);
                if (memoryItem != null && !dumpAchievement.MemoryAddresses.Contains(memoryItem))
                    dumpAchievement.MemoryAddresses.Add(memoryItem);
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

            public bool IsLookup { get; set; }
            
            public bool IsRichPresence { get; set; }

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
            {
                var item = sender as DumpAchievementItem;
                if (item != null)
                {
                    if (item.IsRichPresence && item.IsSelected)
                    {
                        foreach (var achievement in _achievements)
                        {
                            if (achievement.IsLookup)
                                achievement.IsSelected = true;
                        }
                    }
                    else if (item.IsLookup && !item.IsSelected)
                    {
                        foreach (var achievement in _achievements)
                        {
                            if (achievement.IsRichPresence)
                                achievement.IsSelected = false;
                        }
                    }
                }

                UpdateMemoryGrid();
            }
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
            foreach (var achievement in _achievements)
                achievement.IsSelected = false;
        }

        public CommandBase CheckWithTicketsCommand { get; private set; }
        private void CheckWithTickets()
        {
            foreach (var achievement in _achievements)
                achievement.IsSelected = (achievement.OpenTicketCount > 0);
        }

        public enum CodeNoteFilter
        {
            None = 0,
            All,
            ForSelectedAchievements,
        }

        public enum NoteDump
        {
            None = 0,
            All,
            OnlyForDefinedMethods,
        }

        public class CodeNoteFilterLookupItem
        {
            public CodeNoteFilterLookupItem(CodeNoteFilter id, string label)
            {
                Id = id;
                Label = label;
            }
            public CodeNoteFilter Id { get; private set; }
            public string Label { get; private set; }
        }
        public IEnumerable<CodeNoteFilterLookupItem> CodeNoteFilters { get; private set; }

        public class NoteDumpLookupItem
        {
            public NoteDumpLookupItem(NoteDump id, string label)
            {
                Id = id;
                Label = label;
            }
            public NoteDump Id { get; private set; }
            public string Label { get; private set; }
        }
        public IEnumerable<NoteDumpLookupItem> NoteDumps { get; private set; }

        public static readonly ModelProperty SelectedCodeNotesFilterProperty = 
            ModelProperty.Register(typeof(NewScriptDialogViewModel), "SelectedCodeNotesFilter", typeof(CodeNoteFilter), CodeNoteFilter.ForSelectedAchievements, OnSelectedCodeNotesFilterChanged);
        public CodeNoteFilter SelectedCodeNotesFilter
        {
            get { return (CodeNoteFilter)GetValue(SelectedCodeNotesFilterProperty); }
            set { SetValue(SelectedCodeNotesFilterProperty, value); }
        }

        private static void OnSelectedCodeNotesFilterChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            ((NewScriptDialogViewModel)sender).UpdateMemoryGrid();
        }

        public static readonly ModelProperty SelectedNoteDumpProperty = ModelProperty.Register(typeof(NewScriptDialogViewModel), "SelectedNoteDump", typeof(NoteDump), NoteDump.OnlyForDefinedMethods);
        public NoteDump SelectedNoteDump
        {
            get { return (NoteDump)GetValue(SelectedNoteDumpProperty); }
            set { SetValue(SelectedNoteDumpProperty, value); }
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
            if (SelectedCodeNotesFilter == CodeNoteFilter.None)
            {
                MemoryAddressesLabel = "No memory addresses";
            }
            else
            {
                if (SelectedCodeNotesFilter == CodeNoteFilter.All)
                {
                    MemoryAddressesLabel = "All known memory addresses";
                    foreach (var memoryItem in _memoryItems)
                        visibleAddresses.Add(memoryItem);

                    // will merge in any non-byte references from selected achievements
                }
                else
                {
                    MemoryAddressesLabel = (string)MemoryAddressesLabelProperty.DefaultValue;
                    // will merge in all references from selected achievements
                }

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
            }

            // update the grid
            var rowsToRemove = new List<MemoryItem>();
            foreach (var row in MemoryAddresses.Rows)
            {
                var memoryItem = (MemoryItem)row.Model;
                if (!visibleAddresses.Remove(memoryItem))
                    rowsToRemove.Add(memoryItem);
            }
            foreach (var memoryItem in rowsToRemove)
                MemoryAddresses.RemoveRow(memoryItem);

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

        public GameViewModel Finalize()
        {
            var cleansed = _game.Title;
            foreach (var c in Path.GetInvalidFileNameChars())
                cleansed = cleansed.Replace(c.ToString(), "");
            if (String.IsNullOrEmpty(cleansed))
                cleansed = _game.GameId.ToString();
            _game.Script.Filename = cleansed + ".rascript";

            var memoryStream = new MemoryStream();
            Dump(memoryStream);
            _game.Script.SetContent(Encoding.UTF8.GetString(memoryStream.ToArray()));
            _game.Script.SetModified();

            return _game;
        }

        private static string EscapeString(string input)
        {
            return input.Replace("\"", "\\\"");
        }

        private void Dump(Stream outStream)
        {
            MemoryAddresses.Commit();

            using (var stream = new StreamWriter(outStream))
            {
                stream.Write("// ");
                stream.WriteLine(_game.Title);
                stream.Write("// #ID = ");
                stream.WriteLine(String.Format("{0}", _game.GameId));

                bool needLine = true;
                bool hadFunction = false;
                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
                string addressFormat = "{0:X4}";
                if (_memoryItems.Count > 0 && _memoryItems[_memoryItems.Count - 1].Address > 0xFFFF)
                    addressFormat = "{0:X6}"; // TODO: addressFormat is only used in note comments - also apply to generated code

                var lookupsToDump = _achievements.Where(a => a.IsLookup && a.IsSelected).ToList();

                bool first;
                var dumpNotes = SelectedNoteDump;
                var filter = SelectedCodeNotesFilter;
                uint previousNoteAddress = UInt32.MaxValue;
                foreach (var memoryItem in _memoryItems)
                {
                    if (filter == CodeNoteFilter.ForSelectedAchievements)
                    {
                        if (MemoryAddresses.GetRow(memoryItem) == null)
                            continue;
                    }

                    string notes = null;
                    if (dumpNotes != NoteDump.None)
                    {
                        if (_game.Notes.TryGetValue((int)memoryItem.Address, out notes))
                        {
                            if (String.IsNullOrEmpty(memoryItem.FunctionName))
                            {
                                if (dumpNotes == NoteDump.OnlyForDefinedMethods)
                                    continue;

                                if (memoryItem.Address == previousNoteAddress)
                                    continue;
                            }
                        }
                    }

                    if (!String.IsNullOrEmpty(notes))
                    {
                        notes = notes.Trim();
                        if (notes.Length > 0)
                        {
                            if (needLine || hadFunction || !String.IsNullOrEmpty(memoryItem.FunctionName))
                            {
                                needLine = false;
                                stream.WriteLine();
                            }

                            var lines = notes.Split('\n');
                            stream.Write("// $");

                            previousNoteAddress = memoryItem.Address;
                            var address = String.Format(addressFormat, memoryItem.Address);
                            stream.Write(address);
                            stream.Write(": ");
                            stream.WriteLine(lines[0].Trim());

                            for (int i = 1; i < lines.Length; i++)
                            {
                                stream.Write("//        ");
                                if (address.Length > 4)
                                    stream.Write("   ".ToCharArray(), 0, address.Length - 4);
                                stream.WriteLine(lines[i].Trim());
                            }
                        }
                    }

                    if (!String.IsNullOrEmpty(memoryItem.FunctionName))
                    {
                        if (needLine)
                        {
                            needLine = false;
                            stream.WriteLine();
                        }
                        hadFunction = true;

                        if (memoryItem.FunctionName.EndsWith("()"))
                            memoryItem.FunctionName = memoryItem.FunctionName.Substring(0, memoryItem.FunctionName.Length - 2);

                        stream.Write("function ");
                        stream.Write(memoryItem.FunctionName);
                        stream.Write("() => ");
                        var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                        stream.WriteLine(memoryReference);

                        foreach (var dumpLookup in lookupsToDump)
                        {
                            if (dumpLookup.MemoryAddresses.Count == 1 && dumpLookup.MemoryAddresses[0].Address == memoryItem.Address && dumpLookup.MemoryAddresses[0].Size == memoryItem.Size)
                            {
                                DumpLookup(stream, dumpLookup);
                                lookupsToDump.Remove(dumpLookup);
                                break;
                            }
                        }
                    }
                    else
                    {
                        hadFunction = false;
                    }
                }

                foreach (var dumpLookup in lookupsToDump)
                    DumpLookup(stream, dumpLookup);

                foreach (var dumpAchievement in _achievements.Where(a => a.IsSelected && !a.IsLookup && !a.IsRichPresence))
                {
                    var achievement = _game.Editors.FirstOrDefault(a => a.Id == dumpAchievement.Id) as GeneratedAchievementViewModel;
                    if (achievement == null)
                        continue;

                    stream.WriteLine();

                    foreach (var ticket in dumpAchievement.OpenTickets)
                    {
                        string notes;
                        if (_ticketNotes.TryGetValue(ticket, out notes))
                        {
                            var lines = notes.Replace("<br/>", "\n").Split('\n');

                            stream.Write("// Ticket ");
                            stream.Write(ticket);
                            stream.Write(": ");

                            first = true;
                            const int MaxLength = 103; // 120 - "// Ticket XXXXX: ".Length 
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (first)
                                    first = false;
                                else
                                    stream.Write("//               ");

                                var line = lines[i].Trim();
                                if (line.Length == 0)
                                {
                                    stream.WriteLine("");
                                    continue;
                                }

                                while (line.Length > MaxLength)
                                {
                                    var index = line.LastIndexOf(' ', MaxLength - 1);
                                    if (index < 0)
                                    {
                                        stream.WriteLine(line);
                                        line = "";
                                    }
                                    else
                                    {
                                        var front = line.Substring(0, index).Trim();
                                        stream.WriteLine(front);
                                        line = line.Substring(index + 1).Trim();

                                        if (line.Length > 0)
                                            stream.Write("//               ");
                                    }
                                }

                                if (line.Length > 0)
                                    stream.WriteLine(line);
                            }

                            if (first)
                                stream.WriteLine();
                        }
                    }

                    stream.WriteLine("achievement(");

                    var achievementViewModel = (achievement.Core.Achievement != null) ? achievement.Core : achievement.Unofficial;
                    var achievementData = achievementViewModel.Achievement;

                    stream.Write("    title = \"");
                    stream.Write(EscapeString(achievementData.Title));
                    stream.Write("\", description = \"");
                    stream.Write(EscapeString(achievementData.Description));
                    stream.Write("\", points = ");
                    stream.Write(achievementData.Points);
                    stream.WriteLine(",");

                    stream.Write("    id = ");
                    stream.Write(achievementData.Id);
                    stream.Write(", badge = \"");
                    stream.Write(achievementData.BadgeName);
                    stream.Write("\", published = \"");
                    stream.Write(achievementData.Published);
                    stream.Write("\", modified = \"");
                    stream.Write(achievementData.LastModified);
                    stream.WriteLine("\",");

                    stream.Write("    trigger = ");
                    const int indent = 14; // "    trigger = ".length

                    DumpTrigger(stream, numberFormat, dumpAchievement, achievementViewModel, indent);
                    stream.WriteLine();

                    stream.WriteLine(")");
                }

                foreach (var dumpRichPresence in _achievements.Where(a => a.IsRichPresence && a.IsSelected))
                {
                    var displayMacro = _macros.FirstOrDefault(m => m.DisplayLines != null);
                    DumpRichPresence(stream, displayMacro, dumpRichPresence, numberFormat);
                }
            }
        }

        private void DumpLookup(StreamWriter stream, DumpAchievementItem dumpLookup)
        {
            var macro = _macros.FirstOrDefault(m => m.Name == dumpLookup.Label);
            if (macro == null)
                return;

            stream.WriteLine();
            stream.Write(dumpLookup.Label);
            stream.WriteLine("Lookup = {");
            foreach (var entry in macro.LookupEntries)
            {
                if (entry.Key == "*")
                    continue;

                stream.Write("    ");
                stream.Write(entry.Key);
                stream.Write(": \"");
                stream.Write(EscapeString(entry.Value));
                stream.WriteLine("\",");
            }
            stream.WriteLine("}");
        }

        private void DumpRichPresence(StreamWriter stream, RichPresenceMacro displayMacro, DumpAchievementItem dumpRichPresence, NumberFormat numberFormat)
        {
            int index;

            foreach (var line in displayMacro.DisplayLines)
            {
                string displayString = line;
                stream.WriteLine();

                if (line[0] == '?')
                {
                    index = line.IndexOf('?', 1);
                    if (index != -1)
                    {
                        var trigger = line.Substring(1, index - 1);
                        var achievement = new AchievementBuilder();
                        achievement.ParseRequirements(Tokenizer.CreateTokenizer(trigger));

                        var vmAchievement = new AchievementViewModel(_game, "RichPresence");
                        vmAchievement.LoadAchievement(achievement.ToAchievement());

                        stream.Write("rich_presence_conditional_display(");
                        DumpTrigger(stream, numberFormat, dumpRichPresence, vmAchievement, 4);
                        stream.Write(", \"");
                    }

                    ++index;
                }
                else
                {
                    stream.Write("rich_presence_display(\"");
                    index = 0;
                }

                var macros = new List<KeyValuePair<string, string>>();
                do
                {
                    var index1 = displayString.IndexOf('@', index);
                    if (index1 == -1)
                    {
                        stream.Write(displayString.Substring(index));
                        break;
                    }

                    if (index1 > index)
                        stream.Write(displayString.Substring(index, index1 - index));

                    stream.Write('{');
                    stream.Write(macros.Count());
                    stream.Write('}');

                    var index2 = displayString.IndexOf('(', index1);
                    var index3 = displayString.IndexOf(')', index2);

                    var name = displayString.Substring(index1 + 1, index2 - index1 - 1);
                    var parameter = displayString.Substring(index2 + 1, index3 - index2 - 1);

                    macros.Add(new KeyValuePair<string, string>(name, parameter));

                    index = index3 + 1;
                } while (true);

                stream.Write('"');

                foreach (var kvp in macros)
                {
                    stream.WriteLine(",");
                    stream.Write("    ");

                    var macro = _macros.FirstOrDefault(m => m.Name == kvp.Key);
                    if (macro.LookupEntries != null)
                    {
                        stream.Write("rich_presence_lookup(\"");
                        stream.Write(macro.Name);
                        stream.Write("\", ");
                    }
                    else
                    {
                        stream.Write("rich_presence_value(\"");
                        stream.Write(macro.Name);
                        stream.Write("\", ");
                    }

                    var parameter = kvp.Value;
                    if (parameter[1] == ':')
                    {
                        var achievement = new AchievementBuilder();
                        achievement.ParseRequirements(Tokenizer.CreateTokenizer(parameter));

                        if (achievement.CoreRequirements.Count > 0 && achievement.CoreRequirements.Last().Type == RequirementType.Measured)
                            achievement.CoreRequirements.Last().Type = RequirementType.None;

                        var vmAchievement = new AchievementViewModel(_game, "Rich Presence");
                        vmAchievement.LoadAchievement(achievement.ToAchievement());

                        DumpTrigger(stream, numberFormat, dumpRichPresence, vmAchievement, 32);
                    }
                    else
                    {
                        DumpLegacyExpression(stream, parameter, dumpRichPresence);
                    }

                    if (macro.LookupEntries != null)
                    { 
                        stream.Write(", ");
                        stream.Write(macro.Name);
                        stream.Write("Lookup");

                        string defaultEntry;
                        if (macro.LookupEntries.TryGetValue("*", out defaultEntry))
                        {
                            stream.Write(", fallback=\"");
                            stream.Write(EscapeString(defaultEntry));
                            stream.Write('"');
                        }    

                        stream.Write(')');
                    }
                    else
                    {
                        if (macro.FormatType != ValueFormat.Value)
                        {
                            stream.Write(", format=\"");
                            stream.Write(Leaderboard.GetFormatString(macro.FormatType));
                            stream.Write('"');
                        }

                        stream.Write(')');
                    }
                }

                if (macros.Count() > 0)
                    stream.WriteLine();

                stream.WriteLine(')');
            }
        }

        private static void DumpLegacyExpression(StreamWriter stream, string parameter, DumpAchievementItem dumpRichPresence)
        {
            var builder = new StringBuilder();

            var parts = parameter.Split('_');
            for (int i = 0; i < parts.Length; ++i)
            {
                if (i > 0)
                    builder.Append(" + ");

                var operands = parts[i].Split('*');
                for (int j = 0; j < operands.Length; ++j)
                {
                    if (j > 0)
                        builder.Append(" * ");

                    var operand = operands[j];
                    if (operand[0] == 'v' || operand[0] == 'V')
                    {
                        operand = operand.Substring(1);
                        if (operand[0] == '-')
                        {
                            if (builder[builder.Length - 2] == '+')
                            {
                                builder[builder.Length - 2] = '-';
                                operand = operand.Substring(1);
                            }
                        }

                        builder.Append(operand);
                    }
                    else if (operand[0] == 'h' || operand[0] == 'H')
                    {
                        builder.Append("0x");
                        builder.Append(operand.Substring(1));
                    }
                    else if (operand.Length > 2 && operand[1] == '.' && operand[0] == '0' && builder[builder.Length - 2] == '*')
                    {
                        bool isDivisor = false;
                        var f = Double.Parse(operand);
                        if (f > 0.0)
                        {
                            var divisor = 1 / f;
                            if (Math.Abs(Math.Round(divisor) - divisor) < 0.000001)
                            {
                                isDivisor = true;
                                builder[builder.Length - 2] = '/';
                                builder.Append((int)divisor);
                            }
                        }

                        if (!isDivisor)
                            builder.Append(operand);
                    }
                    else
                    {
                        bool isBCD = false;
                        if (operand[0] == 'b')
                        {
                            operand = operand.Substring(1);
                            isBCD = true;
                            builder.Append("bcd(");
                        }

                        var field = Field.Deserialize(Tokenizer.CreateTokenizer(operand));
                        if (field.IsMemoryReference)
                        {
                            var memoryItem = dumpRichPresence.MemoryAddresses.FirstOrDefault(m => m.Address == field.Value && m.Size == field.Size);
                            if (memoryItem != null && !String.IsNullOrEmpty(memoryItem.FunctionName))
                            {
                                builder.Append(memoryItem.FunctionName);
                                builder.Append("()");
                            }
                            else
                            {
                                builder.Append(Field.GetMemoryReference(field.Value, field.Size));
                            }
                        }
                        else
                        {
                            builder.Append(operand);
                        }

                        if (isBCD)
                            builder.Append(')');
                    }
                }
            }

            stream.Write(builder.ToString());
        }

        private static void DumpTrigger(StreamWriter stream, NumberFormat numberFormat, DumpAchievementItem dumpAchievement, AchievementViewModel achievementViewModel, int indent)
        {
            var groupEnumerator = achievementViewModel.RequirementGroups.GetEnumerator();
            groupEnumerator.MoveNext();

            bool isCoreEmpty = !groupEnumerator.Current.Requirements.Any();
            if (!isCoreEmpty)
                DumpPublishedRequirements(stream, dumpAchievement, groupEnumerator.Current, numberFormat, indent);

            bool first = true;
            while (groupEnumerator.MoveNext())
            {
                if (first)
                {
                    if (!isCoreEmpty)
                    {
                        stream.WriteLine(" &&");
                        stream.Write(new string(' ', indent));
                    }
                    stream.Write('(');
                    first = false;

                    if (achievementViewModel.RequirementGroups.Count() == 2)
                    {
                        // only core and one alt, inject an always_false clause to prevent the compiler from joining them
                        stream.Write("always_false() || ");
                    }

                    stream.Write('(');
                }
                else
                {
                    stream.WriteLine(" ||");
                    stream.Write(new string(' ', indent));
                    stream.Write(" (");
                }

                DumpPublishedRequirements(stream, dumpAchievement, groupEnumerator.Current, numberFormat, indent + 2);
                stream.Write(")");
            }
            if (!first)
                stream.Write(')');
        }

        private static void DumpPublishedRequirements(StreamWriter stream, DumpAchievementItem dumpAchievement, 
            RequirementGroupViewModel requirementGroupViewModel, NumberFormat numberFormat, int indent)
        {
            const int MaxWidth = 120;

            var definition = new StringBuilder();
            Parser.AchievementBuilder.AppendStringGroup(definition, 
                requirementGroupViewModel.Requirements.Select(r => r.Requirement), numberFormat, MaxWidth, indent);

            foreach (var memoryItem in dumpAchievement.MemoryAddresses.Where(m => !String.IsNullOrEmpty(m.FunctionName)))
            {
                var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                var functionCall = memoryItem.FunctionName + "()";
                definition.Replace(memoryReference, functionCall);
            }

            stream.Write(definition.ToString());
        }
    }
}
