﻿using Jamiras.Commands;
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
                    return;
                }
            }

            MessageBoxViewModel.ShowMessage("Could not locate notes file for game " + gameId + ".\n\n" +
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
                    addressFormat = "{0:X6}";

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

                        stream.Write("function ");
                        stream.Write(memoryItem.FunctionName);
                        stream.Write("() => ");
                        var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                        stream.WriteLine(memoryReference);
                    }
                    else
                    {
                        hadFunction = false;
                    }
                }

                foreach (var dumpAchievement in _achievements)
                {
                    if (!dumpAchievement.IsSelected)
                        continue;

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
                                while (line.Length > MaxLength)
                                {
                                    var index = line.LastIndexOf(' ', MaxLength - 1);
                                    var front = line.Substring(0, index).Trim();
                                    stream.WriteLine(front);
                                    line = line.Substring(index + 1).Trim();

                                    if (line.Length > 0)
                                        stream.Write("//               ");
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

                    var groupEnumerator = achievementViewModel.RequirementGroups.GetEnumerator();
                    groupEnumerator.MoveNext();
                    stream.Write("    trigger = ");
                    DumpPublishedRequirements(stream, dumpAchievement, groupEnumerator.Current, numberFormat);
                    first = true;
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

                        DumpPublishedRequirements(stream, dumpAchievement, groupEnumerator.Current, numberFormat);
                        stream.Write(")");
                    }
                    if (!first)
                        stream.Write(')');

                    stream.WriteLine();

                    stream.WriteLine(")");
                }
            }
        }

        private void DumpPublishedRequirements(StreamWriter stream, DumpAchievementItem dumpAchievement, 
            RequirementGroupViewModel requirementGroupViewModel, NumberFormat numberFormat)
        {
            bool needsAmpersand = false;
            const int MaxWidth = 106; // 120 - "    trigger = ".Length
            int width = MaxWidth;

            var requirementEnumerator = requirementGroupViewModel.Requirements.GetEnumerator();
            while (requirementEnumerator.MoveNext())
            {
                if (String.IsNullOrEmpty(requirementEnumerator.Current.Definition))
                    continue;

                var addSources = new StringBuilder();
                var subSources = new StringBuilder();
                var addHits = new StringBuilder();
                bool isCombining = true;
                do
                {
                    switch (requirementEnumerator.Current.Requirement.Type)
                    {
                        case RequirementType.AddSource:
                            requirementEnumerator.Current.Requirement.Left.AppendString(addSources, numberFormat);
                            addSources.Append(" + ");
                            break;

                        case RequirementType.SubSource:
                            subSources.Append(" - ");
                            requirementEnumerator.Current.Requirement.Left.AppendString(subSources, numberFormat);
                            break;

                        case RequirementType.AddHits:
                            requirementEnumerator.Current.Requirement.AppendString(addHits, numberFormat);
                            addHits.Append(" || ");
                            break;

                        default:
                            isCombining = false;
                            break;
                    }

                    if (!isCombining)
                        break;

                    if (!requirementEnumerator.MoveNext())
                        return;
                } while (true);

                var definition = new StringBuilder();
                requirementEnumerator.Current.Requirement.AppendString(definition, numberFormat, 
                    addSources.Length > 0 ? addSources.ToString() : null, 
                    subSources.Length > 0 ? subSources.ToString() : null,
                    addHits.Length > 0 ? addHits.ToString() : null);

                foreach (var memoryItem in dumpAchievement.MemoryAddresses.Where(m => !String.IsNullOrEmpty(m.FunctionName)))
                {
                    var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                    var functionCall = memoryItem.FunctionName + "()";
                    definition.Replace(memoryReference, functionCall);
                }

                if (needsAmpersand)
                {
                    stream.Write(" && ");
                    width -= 4;
                }
                else
                {
                    needsAmpersand = true;
                }

                while (definition.Length > MaxWidth)
                {
                    var index = width;
                    while (index > 0 && definition[index] != ' ')
                        index--;

                    stream.Write(definition.ToString().Substring(0, index));
                    stream.WriteLine();
                    stream.Write("              ");
                    definition.Remove(0, index);
                    width = MaxWidth;
                }

                if (width - definition.Length < 0)
                {
                    stream.WriteLine();
                    stream.Write("              ");
                    width = MaxWidth;
                }

                width -= definition.Length;
                stream.Write(definition.ToString());
            }
        }
    }
}
