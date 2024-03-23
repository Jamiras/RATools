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
            : this(ServiceRepository.Instance.FindService<ISettings>(),
                   ServiceRepository.Instance.FindService<IDialogService>(),
                   ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools"),
                   ServiceRepository.Instance.FindService<IFileSystemService>())
        {

        }

        internal NewScriptDialogViewModel(ISettings settings, IDialogService dialogService,
            ILogger logger, IFileSystemService fileSystemService)
            : base(dialogService)
        {
            _settings = settings;
            _logger = logger;
            _fileSystemService = fileSystemService;

            DialogTitle = "New Script";
            CanClose = true;

            SearchCommand = new DelegateCommand(Search);
            CheckAllCommand = new DelegateCommand(CheckAll);
            UncheckAllCommand = new DelegateCommand(UncheckAll);
            CheckWithTicketsCommand = new DelegateCommand(CheckWithTickets);

            CanCheckWithTickets = (!String.IsNullOrEmpty(settings.ApiKey) && !String.IsNullOrEmpty(settings.UserName));

            GameId = new IntegerFieldViewModel("Game _ID", 1, 999999);

            _assets = new ObservableCollection<DumpAsset>();
            _memoryItems = new List<MemoryItem>();
            _ticketNotes = new TinyDictionary<int, string>();
            _macros = new List<RichPresenceMacro>();

            CodeNoteFilters = new[]
            {
                new CodeNoteFilterLookupItem(CodeNoteFilter.All, "All"),
                new CodeNoteFilterLookupItem(CodeNoteFilter.ForSelectedAssets, "For Selected Assets"),
            };

            NoteDumps = new[]
            {
                new NoteDumpLookupItem(NoteDump.None, "None"),
                new NoteDumpLookupItem(NoteDump.All, "All"),
                new NoteDumpLookupItem(NoteDump.OnlyForDefinedMethods, "Only for Functions"),
            };

            FunctionNameStyles = new[]
            {
                new FunctionNameStyleLookupItem(FunctionNameStyle.None, "None"),
                new FunctionNameStyleLookupItem(FunctionNameStyle.SnakeCase, "snake_case"),
                new FunctionNameStyleLookupItem(FunctionNameStyle.CamelCase, "camelCase"),
                new FunctionNameStyleLookupItem(FunctionNameStyle.PascalCase, "PascalCase"),
            };

            MemoryAddresses = new GridViewModel();
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Size", MemoryItem.SizeProperty, new DelegateConverter(a => Field.GetSizeFunction((FieldSize)a), null)) { Width = 48 });
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Address", MemoryItem.AddressProperty, new DelegateConverter(a => String.Format("0x{0:X6}", a), null)) { Width = 56 });
            MemoryAddresses.Columns.Add(new TextColumnDefinition("Function Name", MemoryItem.FunctionNameProperty, new StringFieldMetadata("Function Name", 40, StringFieldAttributes.Required)) { Width = 120 });
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Notes", MemoryItem.NotesProperty));
        }

        private readonly ISettings _settings;
        private readonly IFileSystemService _fileSystemService;
        private readonly ILogger _logger;

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
            foreach (var directory in _settings.EmulatorDirectories)
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

                    var userFile = Path.Combine(dataDirectory, gameId + "-User.txt");
                    if (File.Exists(userFile))
                        LoadUserFile(userFile);

                    return;
                }
            }

            TaskDialogViewModel.ShowWarningMessage("Could not locate code notes for game " + gameId,
                "The game does not appear to have been recently loaded in any of the emulators specified in the Settings dialog.");
            return;
        }

        private void LoadGame(int gameId, string raCacheDirectory)
        {
            _game = new GameViewModel(gameId, "", _logger, _fileSystemService);
            _game.AssociateRACacheDirectory(raCacheDirectory);
            _game.PopulateEditorList(null);
            DialogTitle = "New Script - " + _game.Title;

            _assets.Clear();
            _ticketNotes.Clear();
            _memoryItems.Clear();
            MemoryAddresses.Rows.Clear();

            LoadAchievements();
            LoadLeaderboards();
            LoadNotes();

            if (_assets.Count == 0)
                SelectedCodeNotesFilter = CodeNoteFilter.All;

            UpdateMemoryGrid();

            IsGameLoaded = true;
            GameId.IsEnabled = false;

            if (_assets.Count > 0 && CanCheckWithTickets)
                ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(MergeOpenTickets);
        }

        private void LoadUserFile(string userFile)
        {
            var assets = new LocalAssets(userFile, _fileSystemService);

            if (assets.Achievements.Any())
            {
                var achievementViewModel = new AchievementViewModel(null);
                foreach (var achievement in assets.Achievements)
                {
                    var dumpAchievement = new DumpAsset(achievement.Id, achievement.Title)
                    {
                        Type = DumpAssetType.Achievement,
                        ViewerImage = achievementViewModel.ViewerImage,
                        ViewerType = "Local Achievement",
                        IsUnofficial = true
                    };

                    achievementViewModel.Local.Asset = achievement;
                    AddMemoryReferences(achievementViewModel.Local, dumpAchievement);

                    dumpAchievement.PropertyChanged += DumpAsset_PropertyChanged;
                    _assets.Add(dumpAchievement);
                }
            }

            if (assets.Leaderboards.Any())
            {
                var leaderboardViewModel = new LeaderboardViewModel(null);
                foreach (var leaderboard in assets.Leaderboards)
                {
                    var dumpLeaderboard = new DumpAsset(leaderboard.Id, leaderboard.Title)
                    {
                        Type = DumpAssetType.Leaderboard,
                        ViewerImage = leaderboardViewModel.ViewerImage,
                        ViewerType = "Local Leaderboard",
                        IsUnofficial = true
                    };

                    leaderboardViewModel.Local.Asset = leaderboard;
                    AddMemoryReferences(leaderboardViewModel.Local, dumpLeaderboard);

                    dumpLeaderboard.PropertyChanged += DumpAsset_PropertyChanged;
                    _assets.Add(dumpLeaderboard);
                }
            }
        }

        private void AddMemoryReferences(AssetSourceViewModel asset, DumpAsset dumpAsset)
        {
            foreach (var trigger in asset.TriggerList)
            {
                foreach (var group in trigger.Groups)
                {
                    foreach (var requirement in group.Requirements)
                    {
                        if (requirement.Requirement == null)
                            continue;

                        if (requirement.Requirement.Left.IsMemoryReference)
                        {
                            var memoryItem = AddMemoryAddress(requirement.Requirement.Left);
                            if (memoryItem != null && !dumpAsset.MemoryAddresses.Contains(memoryItem))
                                dumpAsset.MemoryAddresses.Add(memoryItem);
                        }

                        if (requirement.Requirement.Right.IsMemoryReference)
                        {
                            var memoryItem = AddMemoryAddress(requirement.Requirement.Right);
                            if (memoryItem != null && !dumpAsset.MemoryAddresses.Contains(memoryItem))
                                dumpAsset.MemoryAddresses.Add(memoryItem);
                        }
                    }
                }
            }
        }

        private void LoadAchievements()
        {
            var unofficialAchievements = new List<DumpAsset>();
            foreach (var achievement in _game.Editors.OfType<AchievementViewModel>())
            {
                var publishedAchievement = achievement.Published.Asset as Achievement;
                if (publishedAchievement == null)
                    continue;

                var dumpAchievement = new DumpAsset(achievement.Id, publishedAchievement.Title)
                {
                    Type = DumpAssetType.Achievement,
                    ViewerImage = achievement.ViewerImage,
                    ViewerType = achievement.ViewerType
                };

                if (publishedAchievement.IsUnofficial)
                {
                    dumpAchievement.IsUnofficial = true;
                    dumpAchievement.ViewerType = "Unofficial Achievement";
                    unofficialAchievements.Add(dumpAchievement);
                }
                else
                {
                    _assets.Add(dumpAchievement);
                }

                AddMemoryReferences(achievement.Published, dumpAchievement);

                dumpAchievement.IsSelected = true;
                dumpAchievement.PropertyChanged += DumpAsset_PropertyChanged;
            }

            foreach (var unofficialAchievement in unofficialAchievements)
                _assets.Add(unofficialAchievement);
        }

        private void LoadLeaderboards()
        {
            foreach (var leaderboard in _game.Editors.OfType<LeaderboardViewModel>())
            {
                var publishedLeaderboard = leaderboard.Published.Asset as Leaderboard;
                if (publishedLeaderboard == null)
                    continue;

                var dumpLeaderboard = new DumpAsset(leaderboard.Id, publishedLeaderboard.Title)
                {
                    Type = DumpAssetType.Leaderboard,
                    ViewerImage = leaderboard.ViewerImage,
                    ViewerType = leaderboard.ViewerType
                };

                _assets.Add(dumpLeaderboard);

                AddMemoryReferences(leaderboard.Published, dumpLeaderboard);

                dumpLeaderboard.IsSelected = true;
                dumpLeaderboard.PropertyChanged += DumpAsset_PropertyChanged;
            }
        }

        private void LoadNotes()
        {
            foreach (var kvp in _game.Notes)
            {
                var note = new CodeNote((uint)kvp.Key, kvp.Value);
                var size = (note.FieldSize == FieldSize.None) ? FieldSize.Byte : note.FieldSize;
                AddMemoryAddress(new Field { Size = size, Type = FieldType.MemoryAddress, Value = note.Address });
            }
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
                    else if (currentMacro != null)
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
                    var dumpLookup = new DumpAsset(0, macro.Name)
                    {
                        Type = DumpAssetType.Lookup,
                        ViewerImage = "/RATools;component/Resources/script.png",
                        ViewerType = "Lookup"
                    };
                    dumpLookup.PropertyChanged += DumpAsset_PropertyChanged;
                    _assets.Add(dumpLookup);
                }
            }

            if (displayMacro != null)
            {
                var dumpRichPresence = new DumpAsset(0, "Rich Presence Script")
                {
                    Type = DumpAssetType.RichPresence,
                    ViewerImage = "/RATools;component/Resources/rich_presence.png",
                    ViewerType = "Rich Presence"
                };

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

                dumpRichPresence.PropertyChanged += DumpAsset_PropertyChanged;
                _assets.Add(dumpRichPresence);
            }
        }

        private void AddMacroMemoryReferences(DumpAsset displayRichPresence, string displayString)
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

                var macro = _assets.FirstOrDefault(a => a.Type == DumpAssetType.Lookup && a.Label == name);
                if (macro == null)
                    macro = displayRichPresence;

                if (parameter.Length > 2 && parameter[1] == ':')
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

                if (macro.Type == DumpAssetType.Lookup)
                {
                    foreach (var address in macro.MemoryAddresses)
                    {
                        if (!displayRichPresence.MemoryAddresses.Contains(address))
                            displayRichPresence.MemoryAddresses.Add(address);
                    }
                }

                index = index2 + 1;
            } while (true);
        }

        private void MergeOpenTickets()
        {
            var ticketsJson = RAWebCache.Instance.GetOpenTicketsForGame(_game.GameId);
            if (ticketsJson == null)
                return;

            foreach (var ticket in ticketsJson.GetField("Tickets").ObjectArrayValue)
            {
                var ticketId = ticket.GetField("ID").IntegerValue.GetValueOrDefault();
                _ticketNotes[ticketId] = ticket.GetField("ReportNotes").StringValue;

                var achievementId = ticket.GetField("AchievementID").IntegerValue.GetValueOrDefault();
                var achievement = _assets.FirstOrDefault(a => a.Id == achievementId && a.Type == DumpAssetType.Achievement);
                if (achievement != null)
                {
                    achievement.OpenTickets.Add(ticketId);
                    achievement.RaiseOpenTicketCountChanged();
                }
            }
        }

        private void AddMemoryReferences(DumpAsset dumpAsset, Requirement requirement)
        {
            if (requirement.Left.IsMemoryReference)
            {
                var memoryItem = AddMemoryAddress(requirement.Left);
                if (memoryItem != null && !dumpAsset.MemoryAddresses.Contains(memoryItem))
                    dumpAsset.MemoryAddresses.Add(memoryItem);
            }

            if (requirement.Right.IsMemoryReference)
            {
                var memoryItem = AddMemoryAddress(requirement.Right);
                if (memoryItem != null && !dumpAsset.MemoryAddresses.Contains(memoryItem))
                    dumpAsset.MemoryAddresses.Add(memoryItem);
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
            if (!_game.Notes.TryGetValue(field.Value, out notes))
                return null;

            var item = new MemoryItem(field.Value, field.Size, notes.Replace("\r\n", " ").Replace('\n', ' '));
            _memoryItems.Insert(index, item);
            return item;
        }

        private GameViewModel _game;
        private readonly TinyDictionary<int, string> _ticketNotes;
        
        public enum DumpAssetType
        {
            None,
            Achievement,
            Leaderboard,
            RichPresence,
            Lookup
        }

        public class DumpAsset : LookupItem
        {
            public DumpAsset(int id, string label)
                : base(id, label)
            {
                OpenTickets = new List<int>();
                MemoryAddresses = new List<MemoryItem>();
            }

            public bool IsUnofficial { get; set; }

            public DumpAssetType Type { get; set; }

            public string ViewerImage { get; set; }

            public string ViewerType { get; set; }

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

        public IEnumerable<DumpAsset> Assets
        {
            get { return _assets; }
        }
        private readonly ObservableCollection<DumpAsset> _assets;
        
        private void DumpAsset_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected")
            {
                var item = sender as DumpAsset;
                if (item != null)
                {
                    if (item.Type == DumpAssetType.RichPresence && item.IsSelected)
                    {
                        foreach (var asset in _assets)
                        {
                            if (asset.Type == DumpAssetType.Lookup)
                                asset.IsSelected = true;
                        }
                    }
                    else if (item.Type == DumpAssetType.Lookup && !item.IsSelected)
                    {
                        foreach (var asset in _assets)
                        {
                            if (asset.Type == DumpAssetType.RichPresence)
                                asset.IsSelected = false;
                        }
                    }
                }

                UpdateMemoryGrid();
            }
        }

        public CommandBase CheckAllCommand { get; private set; }
        private void CheckAll()
        {
            foreach (var asset in _assets)
                asset.IsSelected = true;
        }

        public CommandBase UncheckAllCommand { get; private set; }
        private void UncheckAll()
        {
            foreach (var asset in _assets)
                asset.IsSelected = false;
        }

        public CommandBase CheckWithTicketsCommand { get; private set; }
        public bool CanCheckWithTickets { get; private set; }
        private void CheckWithTickets()
        {
            foreach (var asset in _assets)
                asset.IsSelected = (asset.OpenTicketCount > 0);
        }

        public enum CodeNoteFilter
        {
            None = 0,
            All,
            ForSelectedAssets,
        }

        public enum NoteDump
        {
            None = 0,
            All,
            OnlyForDefinedMethods,
        }

        public enum FunctionNameStyle
        {
            None = 0,
            SnakeCase,  // lower_with_underscore
            PascalCase, // UpperEachFirst
            CamelCase,  // upperInMiddle
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

        public class FunctionNameStyleLookupItem
        {
            public FunctionNameStyleLookupItem(FunctionNameStyle id, string label)
            {
                Id = id;
                Label = label;
            }
            public FunctionNameStyle Id { get; private set; }
            public string Label { get; private set; }
        }
        public IEnumerable<FunctionNameStyleLookupItem> FunctionNameStyles { get; private set; }

        public static readonly ModelProperty SelectedCodeNotesFilterProperty = 
            ModelProperty.Register(typeof(NewScriptDialogViewModel), "SelectedCodeNotesFilter", typeof(CodeNoteFilter), CodeNoteFilter.ForSelectedAssets, OnSelectedCodeNotesFilterChanged);
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

        public static readonly ModelProperty SelectedFunctionNameStyleProperty = ModelProperty.Register(typeof(NewScriptDialogViewModel), "SelectedFunctionNameStyle", typeof(FunctionNameStyle), FunctionNameStyle.None, OnSelectedFunctionNameStyleChanged);
        public FunctionNameStyle SelectedFunctionNameStyle
        {
            get { return (FunctionNameStyle)GetValue(SelectedFunctionNameStyleProperty); }
            set { SetValue(SelectedFunctionNameStyleProperty, value); }
        }

        private static void OnSelectedFunctionNameStyleChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            ((NewScriptDialogViewModel)sender).UpdateFunctionNames();
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

                foreach (var achievement in _assets)
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
            visibleAddresses.Sort((l,r) =>
            {
                int diff = (int)l.Address - (int)r.Address;
                if (diff == 0)
                    diff = (int)l.Size - (int)r.Size;
                return diff;
            });

            var functionNameStyle = SelectedFunctionNameStyle;
            var memIndex = 0;
            for (int addrIndex = 0; addrIndex < visibleAddresses.Count; addrIndex++)
            {
                MemoryItem rowItem = null;
                var memoryItem = visibleAddresses[addrIndex];
                while (memIndex < MemoryAddresses.Rows.Count)
                {
                    rowItem = (MemoryItem)MemoryAddresses.Rows[memIndex].Model;
                    if (rowItem.Address < memoryItem.Address || rowItem.Size < memoryItem.Size)
                    {
                        MemoryAddresses.RemoveRow(rowItem);
                        continue;
                    }

                    break;
                }

                if (functionNameStyle != FunctionNameStyle.None)
                {
                    string note;
                    if (_game.Notes.TryGetValue(memoryItem.Address, out note))
                        memoryItem.UpdateFunctionName(functionNameStyle, note);
                }

                if (rowItem == null || rowItem.Address != memoryItem.Address || rowItem.Size != memoryItem.Size)
                    MemoryAddresses.InsertRow(memIndex, memoryItem);

                memIndex++;
            }

            while (memIndex < MemoryAddresses.Rows.Count)
                MemoryAddresses.Rows.RemoveAt(MemoryAddresses.Rows.Count - 1);
        }

        private void UpdateFunctionNames()
        {
            var functionNameStyle = SelectedFunctionNameStyle;

            foreach (var row in MemoryAddresses.Rows)
            {
                var memoryItem = (MemoryItem)row.Model;

                string note;
                if (_game.Notes.TryGetValue(memoryItem.Address, out note))
                    memoryItem.UpdateFunctionName(functionNameStyle, note);
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

            public void UpdateFunctionName(FunctionNameStyle style, string note)
            {
                if (style == FunctionNameStyle.None)
                {
                    FunctionName = String.Empty;
                    return;
                }

                var index = note.IndexOf('\n');

                var bitSuffix = new StringBuilder();
                switch (Size)
                {
                    case FieldSize.Bit0:
                    case FieldSize.Bit1:
                    case FieldSize.Bit2:
                    case FieldSize.Bit3:
                    case FieldSize.Bit4:
                    case FieldSize.Bit5:
                    case FieldSize.Bit6:
                    case FieldSize.Bit7:
                        var bitSize = Size.ToString();
                        var bitIndex = note.IndexOf(bitSize, StringComparison.OrdinalIgnoreCase);
                        if (bitIndex == -1)
                        {
                            bitSize = "b" + bitSize[3];
                            bitIndex = note.IndexOf(bitSize, StringComparison.OrdinalIgnoreCase);
                        }
                        if (bitIndex == -1)
                            goto case FieldSize.LowNibble;

                        bitIndex += bitSize.Length;
                        if (Char.IsLetterOrDigit(note[bitIndex]))
                            goto case FieldSize.LowNibble;

                        while (bitIndex < note.Length && (Char.IsWhiteSpace(note[bitIndex]) || note[bitIndex] == '=' || note[bitIndex] == ':'))
                            ++bitIndex;

                        if (!Char.IsLetterOrDigit(note[bitIndex]))
                            goto case FieldSize.LowNibble;

                        while (bitIndex < note.Length && note[bitIndex] != '\n' && note[bitIndex] != ',' && note[bitIndex] != ';')
                            bitSuffix.Append(note[bitIndex++]);

                        if (bitSuffix.Length == 0)
                            goto case FieldSize.LowNibble;

                        if (index == -1 && 
                            note.StartsWith("bit", StringComparison.OrdinalIgnoreCase) ||
                            (Char.IsDigit(note[1]) && (note[0] == 'b' || note[0] == 'B')))
                        {
                            // found a bit suffix, but nothing to use as a prefix. discard prefix
                            note = "";
                            index = -1;
                        }
                        break;

                    case FieldSize.LowNibble:
                    case FieldSize.HighNibble:
                        // ignore multi-line note for sizes smaller than a byte
                        if (index != -1 || note.Contains(',') || note.Contains(';'))
                            return;
                        break;
                }

                if (index != -1)
                    note = note.Substring(0, index);
                note = note.Trim();

                // remove size indicator if present
                var startIndex = note.IndexOf('[');
                if (startIndex != -1)
                {
                    var endIndex = note.IndexOf(']', startIndex + 1);
                    if (endIndex != -1)
                    {
                        var size = note.Substring(startIndex + 1, endIndex - startIndex - 1);

                        if (size.Contains("byte", StringComparison.OrdinalIgnoreCase) ||
                            size.Contains("bit", StringComparison.OrdinalIgnoreCase) ||
                            size.Contains("float", StringComparison.OrdinalIgnoreCase) ||
                            size.Contains("MBF", StringComparison.OrdinalIgnoreCase) ||
                            size.Contains('=') || size.Contains(','))
                        {
                            note = note.Substring(0, startIndex) + note.Substring(endIndex + 1);
                        }
                    }
                }

                // remove potential value assigments
                var equalsIndex = note.IndexOfAny(new[] { '=', ':' });
                if (equalsIndex != -1)
                {
                    var left = note.Substring(0, equalsIndex).Trim();
                    var right = note.Substring(equalsIndex + 1).Trim();
                    if (left.Length > 0 && Char.IsDigit(left[0]))
                        note = right;
                    else if (right.Length > 0 && Char.IsDigit(right[0]))
                        note = left;
                }

                if (bitSuffix.Length > 0)
                    note += " - " + bitSuffix.ToString();

                // build the function name
                bool valid = false;
                var functionName = new StringBuilder();

                var newWord = true;
                foreach (var c in note)
                {
                    if (Char.IsLetter(c) || (valid && Char.IsDigit(c)))
                    {
                        valid = true;

                        if (newWord)
                        {
                            newWord = false;

                            switch (style)
                            {
                                case FunctionNameStyle.PascalCase:
                                    functionName.Append(Char.ToUpper(c));
                                    continue;

                                case FunctionNameStyle.CamelCase:
                                    if (functionName.Length != 0)
                                    {
                                        functionName.Append(Char.ToUpper(c));
                                        continue;
                                    }
                                    break;

                                case FunctionNameStyle.SnakeCase:
                                    if (functionName.Length != 0)
                                        functionName.Append('_');
                                    break;
                            }
                        }

                        functionName.Append(Char.ToLower(c));
                    }
                    else if (valid)
                    {
                        newWord = true;
                    }
                }

                if (valid)
                    FunctionName = functionName.ToString();
            }
        }

        private readonly List<MemoryItem> _memoryItems;

        public GameViewModel Finalize()
        {
            _game.InitializeForUI();

            var cleansed = _game.Title ?? "Untitled";
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

        internal void Dump(Stream outStream)
        {
            MemoryAddresses.Commit();

            const int MaxWidth = 120;
            var scriptBuilderContext = new ScriptBuilderContext
            {
                WrapWidth = MaxWidth,
                NumberFormat = _settings.HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal,
            };

            foreach (var memoryItem in _memoryItems.Where(m => !String.IsNullOrEmpty(m.FunctionName)))
            {
                var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                var functionCall = memoryItem.FunctionName + "()";
                scriptBuilderContext.AddAlias(memoryReference, functionCall);
            }

            using (var stream = new StreamWriter(outStream))
            {
                stream.Write("// ");
                stream.WriteLine(_game.Title);
                stream.Write("// #ID = ");
                stream.WriteLine(String.Format("{0}", _game.GameId));

                var lookupsToDump = _assets.Where(a => a.Type == DumpAssetType.Lookup && a.IsSelected).ToList();

                DumpMemoryAccessors(stream, lookupsToDump);

                foreach (var dumpLookup in lookupsToDump)
                    DumpLookup(stream, dumpLookup);

                DumpAchievements(stream, scriptBuilderContext);
                DumpLeaderboards(stream, scriptBuilderContext);

                foreach (var dumpRichPresence in _assets.Where(a => a.Type == DumpAssetType.RichPresence && a.IsSelected))
                {
                    var displayMacro = _macros.FirstOrDefault(m => m.DisplayLines != null);
                    DumpRichPresence(stream, displayMacro, dumpRichPresence, scriptBuilderContext);
                }
            }
        }

        private void DumpMemoryAccessors(StreamWriter stream, List<DumpAsset> lookupsToDump)
        {
            string addressFormat = "{0:X4}";
            if (_memoryItems.Count > 0 && _memoryItems[_memoryItems.Count - 1].Address > 0xFFFF)
                addressFormat = "{0:X6}"; // TODO: addressFormat is only used in note comments - also apply to generated code

            bool needLine = true;
            bool hadFunction = false;

            var dumpNotes = SelectedNoteDump;
            var filter = SelectedCodeNotesFilter;
            uint previousNoteAddress = UInt32.MaxValue;
            foreach (var memoryItem in _memoryItems)
            {
                if (filter == CodeNoteFilter.ForSelectedAssets)
                {
                    if (MemoryAddresses.GetRow(memoryItem) == null)
                        continue;
                }

                string notes = null;
                if (dumpNotes != NoteDump.None)
                {
                    if (_game.Notes.TryGetValue(memoryItem.Address, out notes))
                    {
                        if (String.IsNullOrEmpty(memoryItem.FunctionName))
                        {
                            if (dumpNotes == NoteDump.OnlyForDefinedMethods)
                                continue;

                            if (memoryItem.Address == previousNoteAddress)
                                continue;
                        }
                    }

                    if (!String.IsNullOrEmpty(notes) && memoryItem.Address != previousNoteAddress)
                    {
                        previousNoteAddress = memoryItem.Address;

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
        }

        private void DumpTickets(StreamWriter stream, DumpAsset dumpAsset)
        {
            foreach (var ticket in dumpAsset.OpenTickets)
            {
                string notes;
                if (!_ticketNotes.TryGetValue(ticket, out notes))
                    continue;

                var lines = notes.Replace("<br/>", "\n").Split('\n');

                stream.Write("// Ticket ");
                stream.Write(ticket);
                stream.Write(": ");

                bool first = true;
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

        private void DumpAchievements(StreamWriter stream, ScriptBuilderContext scriptBuilderContext)
        {
            var indentedContext = scriptBuilderContext.Clone();
            indentedContext.Indent = 14; // "    trigger = ".length

            foreach (var dumpAchievement in _assets.Where(a => a.IsSelected && a.Type == DumpAssetType.Achievement))
            {
                var achievementViewModel = _game.Editors.OfType<AchievementViewModel>().FirstOrDefault(a => a.Id == dumpAchievement.Id);
                if (achievementViewModel == null)
                    continue;

                stream.WriteLine();

                DumpTickets(stream, dumpAchievement);

                stream.WriteLine("achievement(");

                var assetSource = (achievementViewModel.Published.Asset != null) ? achievementViewModel.Published : achievementViewModel.Local;
                var achievementData = assetSource.Asset as Achievement;

                stream.Write("    title = \"");
                stream.Write(EscapeString(achievementData.Title));
                stream.Write("\", points = ");
                stream.Write(achievementData.Points);
                if (achievementData.Type != AchievementType.Standard)
                {
                    stream.Write(", type=\"");
                    stream.Write(Achievement.GetTypeString(achievementData.Type));
                    stream.Write("\"");
                }
                stream.WriteLine(",");

                stream.Write("    description = \"");
                stream.Write(EscapeString(achievementData.Description));
                stream.WriteLine("\",");

                if (dumpAchievement.ViewerType != "Local Achievement")
                {
                    stream.Write("    id = ");
                    stream.Write(achievementData.Id);
                    stream.Write(", badge = \"");
                    stream.Write(achievementData.BadgeName);
                    stream.Write("\", published = \"");
                    stream.Write(achievementData.Published);
                    stream.Write("\", modified = \"");
                    stream.Write(achievementData.LastModified);
                    stream.WriteLine("\",");
                }

                stream.Write("    trigger = ");
                DumpTrigger(stream, indentedContext, dumpAchievement, assetSource.TriggerList.First());
                stream.WriteLine();

                stream.WriteLine(")");
            }
        }

        private void DumpLeaderboards(StreamWriter stream, ScriptBuilderContext scriptBuilderContext)
        {
            var indentedContext = scriptBuilderContext.Clone();
            indentedContext.Indent = 13; // "    start  = ".length

            foreach (var dumpLeaderboard in _assets.Where(a => a.IsSelected && a.Type == DumpAssetType.Leaderboard))
            {
                var leaderboardViewModel = _game.Editors.OfType<LeaderboardViewModel>().FirstOrDefault(a => a.Id == dumpLeaderboard.Id);
                if (leaderboardViewModel == null)
                    continue;

                stream.WriteLine();

                DumpTickets(stream, dumpLeaderboard);

                stream.WriteLine("leaderboard(");

                var assetSource = (leaderboardViewModel.Published.Asset != null) ? leaderboardViewModel.Published : leaderboardViewModel.Local;
                var leaderboardData = assetSource.Asset as Leaderboard;

                stream.Write("    id = ");
                stream.Write(leaderboardData.Id);
                stream.Write(", title = \"");
                stream.Write(EscapeString(leaderboardData.Title));
                stream.WriteLine("\",");
                stream.Write("    description = \"");
                stream.Write(EscapeString(leaderboardData.Description));
                stream.WriteLine("\",");

                stream.Write("    start  = ");
                DumpTrigger(stream, indentedContext, dumpLeaderboard, assetSource.TriggerList.First());
                stream.WriteLine(",");

                stream.Write("    cancel = ");
                DumpTrigger(stream, indentedContext, dumpLeaderboard, assetSource.TriggerList.ElementAt(1));
                stream.WriteLine(",");

                stream.Write("    submit = ");
                DumpTrigger(stream, indentedContext, dumpLeaderboard, assetSource.TriggerList.ElementAt(2));
                stream.WriteLine(",");

                stream.Write("    value  = ");
                var valueTrigger = assetSource.TriggerList.ElementAt(3);
                if (valueTrigger.Groups.Count() > 1 ||
                    valueTrigger.Groups.First().Requirements.Any(r => r.Requirement.IsMeasured))
                {
                    DumpValue(stream, indentedContext, dumpLeaderboard, assetSource.TriggerList.ElementAt(3));
                }
                else
                {
                    DumpLegacyExpression(stream, leaderboardData.Value, dumpLeaderboard, indentedContext);
                }
                stream.WriteLine(",");

                stream.Write("    format = \"");
                stream.Write(Leaderboard.GetFormatString(leaderboardData.Format));
                stream.Write("\"");

                if (leaderboardData.LowerIsBetter)
                    stream.Write(", lower_is_better = true");

                stream.WriteLine();
                stream.WriteLine(")");
            }
        }

        private void DumpLookup(StreamWriter stream, DumpAsset dumpLookup)
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

                var value = EscapeString(entry.Value);
                foreach (var part in entry.Key.Split(','))
                {
                    if (part.Contains('-'))
                    {
                        var range = part.Split('-');
                        var start = range[0].StartsWith("0x") ? Int32.Parse(range[0].Substring(2), System.Globalization.NumberStyles.HexNumber) : Int32.Parse(range[0]);
                        var end = range[1].StartsWith("0x") ? Int32.Parse(range[1].Substring(2), System.Globalization.NumberStyles.HexNumber) : Int32.Parse(range[1]);
                        for (int i = start; i <= end; ++i)
                        {
                            stream.Write("    ");
                            stream.Write(i);
                            stream.Write(": \"");
                            stream.Write(value);
                            stream.WriteLine("\",");
                        }
                    }
                    else
                    {
                        stream.Write("    ");
                        stream.Write(part);
                        stream.Write(": \"");
                        stream.Write(value);
                        stream.WriteLine("\",");
                    }
                }
            }
            stream.WriteLine("}");
        }

        private void DumpRichPresence(StreamWriter stream, RichPresenceMacro displayMacro, DumpAsset dumpRichPresence, ScriptBuilderContext scriptBuilderContext)
        {
            int index;
            var notes = new Dictionary<uint, string>();

            var indentedContext = scriptBuilderContext.Clone();
            indentedContext.Indent = 4;

            foreach (var line in displayMacro.DisplayLines)
            {
                string displayString = line;
                stream.WriteLine();

                if (line[0] == '?')
                {
                    index = line.IndexOf('?', 1);
                    if (index != -1)
                    {
                        var trigger = Trigger.Deserialize(line.Substring(1, index - 1));
                        var vmTrigger = new TriggerViewModel("RichPresence", trigger, scriptBuilderContext.NumberFormat, notes);

                        stream.Write("rich_presence_conditional_display(");
                        indentedContext.Indent = 4;
                        DumpTrigger(stream, indentedContext, dumpRichPresence, vmTrigger);
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
                    if (macro == null)
                    {
                        stream.Write("rich_presence_value(\"");
                        stream.Write(kvp.Key);
                        stream.Write("\", ");
                        indentedContext.Indent = 24; // "    rich_presence_value(".length
                    }
                    else if (macro.LookupEntries != null)
                    {
                        stream.Write("rich_presence_lookup(\"");
                        stream.Write(macro.Name);
                        stream.Write("\", ");
                        indentedContext.Indent = 25; // "    rich_presence_lookup(".length
                    }
                    else
                    {
                        stream.Write("rich_presence_value(\"");
                        stream.Write(macro.Name);
                        stream.Write("\", ");
                        indentedContext.Indent = 24; // "    rich_presence_value(".length
                    }

                    var value = Value.Deserialize(kvp.Value);
                    if (value.Values.Any())
                    {
                        var measured = value.Values.First().Requirements.FirstOrDefault(r => r.Type == RequirementType.Measured);
                        if (measured != null)
                            measured.Type = RequirementType.None; // measured() is implicit
                    }
                    DumpLegacyExpression(stream, value, dumpRichPresence, indentedContext);

                    if (macro == null)
                    {
                        var macroFormat = RichPresenceBuilder.GetValueFormat(kvp.Key);
                        if (macroFormat != ValueFormat.None && macroFormat != ValueFormat.Value)
                        {
                            stream.Write(", format=\"");
                            stream.Write(RichPresenceBuilder.GetFormatString(macroFormat));
                            stream.Write('\"');
                        }

                        stream.Write(')');
                    }
                    else if (macro.LookupEntries != null)
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

        private static void DumpLegacyExpression(StreamWriter stream, Value value, DumpAsset dumpAsset, ScriptBuilderContext scriptBuilderContext)
        {
            if (!value.Values.Any())
            {
                stream.Write('0');
                return;
            }

            var script = new ValueBuilder(value).ToScript(scriptBuilderContext);
            if (script.Length > 2 && script[0] == '(' && script[script.Length - 1] == ')')
                script = script.Substring(1, script.Length - 2);

            stream.Write(script);
        }

        private static void DumpTrigger(StreamWriter stream, ScriptBuilderContext scriptBuilderContext, DumpAsset dumpAsset, TriggerViewModel triggerViewModel)
        {
            var triggerWhenMeasuredGroups = new List<RequirementGroupViewModel>();
            if (triggerViewModel.Groups.Count() > 2)
                IdentifyTriggerWhenMeasured(triggerViewModel, triggerWhenMeasuredGroups);

            var groupEnumerator = triggerViewModel.Groups.GetEnumerator();
            groupEnumerator.MoveNext();

            bool isCoreEmpty = !groupEnumerator.Current.Requirements.Any();
            if (!isCoreEmpty)
                DumpPublishedRequirements(stream, dumpAsset, groupEnumerator.Current, scriptBuilderContext);

            bool first = true;
            while (groupEnumerator.MoveNext())
            {
                // ignore trigger alt of triggerWhenMeasured groups
                if (ReferenceEquals(triggerWhenMeasuredGroups.FirstOrDefault(), groupEnumerator.Current))
                    continue;

                if (first)
                {
                    if (!isCoreEmpty)
                    {
                        stream.WriteLine(" &&");
                        stream.Write(new string(' ', scriptBuilderContext.Indent));
                    }
                    stream.Write('(');
                    first = false;

                    if (triggerViewModel.Groups.Count() == 2)
                    {
                        // only core and one alt, inject an always_false clause to prevent the compiler from joining them
                        stream.Write("always_false() || ");
                    }
                }
                else
                {
                    stream.WriteLine(" ||");
                    stream.Write(new string(' ', scriptBuilderContext.Indent));
                    stream.Write(' ');
                }

                if (triggerWhenMeasuredGroups.Contains(groupEnumerator.Current))
                {
                    stream.Write("trigger_when(");
                    scriptBuilderContext.Indent += 2;
                    DumpPublishedRequirements(stream, dumpAsset, groupEnumerator.Current, scriptBuilderContext);
                    scriptBuilderContext.Indent -= 2;
                    stream.Write(')');
                }
                else
                {
                    stream.Write('(');
                    scriptBuilderContext.Indent += 2;
                    DumpPublishedRequirements(stream, dumpAsset, groupEnumerator.Current, scriptBuilderContext);
                    scriptBuilderContext.Indent -= 2;
                    stream.Write(')');
                }
            }
            if (!first)
                stream.Write(')');
        }

        private static void IdentifyTriggerWhenMeasured(TriggerViewModel triggerViewModel, List<RequirementGroupViewModel> triggerWhenMeasuredGroups)
        {
            RequirementEx triggerAlt = null;
            foreach (var group in triggerViewModel.Groups.Skip(1))
            {
                if (!group.Requirements.Any(r => r.Requirement.Type == RequirementType.Trigger))
                    continue;

                var groupEx = RequirementEx.Combine(group.Requirements.Select(r => r.Requirement));
                if (groupEx.Count == 1)
                {
                    triggerWhenMeasuredGroups.Add(group);
                    triggerAlt = groupEx[0];
                    break;
                }
            }

            if (triggerAlt == null)
                return;

            foreach (var group in triggerViewModel.Groups.Skip(1))
            {
                if (!group.Requirements.Any(r => r.Requirement.Type == RequirementType.Measured || r.Requirement.Type == RequirementType.MeasuredPercent))
                    continue;

                var groupEx = RequirementEx.Combine(group.Requirements.Select(r => r.Requirement));
                if (groupEx.Count != 1)
                    continue;

                if (triggerAlt.Evaluate() == false)
                {
                    triggerWhenMeasuredGroups.Add(group);
                }
                else
                {
                    for (int i = 0; i < groupEx.Count; i++)
                    {
                        var lastRequirement = groupEx[i].Requirements.Last();
                        if (lastRequirement.Type == RequirementType.Measured || lastRequirement.Type == RequirementType.MeasuredPercent)
                        {
                            var clone = lastRequirement.Clone();
                            clone.Type = RequirementType.Trigger;
                            groupEx[i].Requirements[groupEx[i].Requirements.Count - 1] = clone;
                            if (groupEx[i] == triggerAlt)
                                triggerWhenMeasuredGroups.Add(group);
                        }
                    }
                }
            }

            // if only the trigger alt was found, discard it
            if (triggerWhenMeasuredGroups.Count == 1)
                triggerWhenMeasuredGroups.Clear();
        }

        private static void DumpValue(StreamWriter stream, ScriptBuilderContext context, DumpAsset dumpAsset, TriggerViewModel triggerViewModel)
        {
            if (triggerViewModel.Groups.Count() > 1)
            {
                stream.WriteLine("max_of(");
                context.Indent += 4;

                bool first = true;
                foreach (var value in triggerViewModel.Groups)
                {
                    if (!first)
                        stream.WriteLine(",");

                    stream.Write(new string(' ', context.Indent));
                    first = false;

                    DumpPublishedRequirements(stream, dumpAsset, value, context, true);
                }

                context.Indent -= 4;
                stream.WriteLine();
                stream.Write(new string(' ', context.Indent));
                stream.Write(")");
            }
            else
            {
                DumpPublishedRequirements(stream, dumpAsset, triggerViewModel.Groups.First(), context, true);
            }
        }

        private static void DumpPublishedRequirements(StreamWriter stream, DumpAsset dumpAsset,
            RequirementGroupViewModel requirementGroupViewModel, ScriptBuilderContext scriptBuilderContext, bool isValue = false)
        {
            var definition = new StringBuilder();
            var context = scriptBuilderContext.Clone();
            context.IsValue = isValue;
            context.AppendRequirements(definition, requirementGroupViewModel.Requirements.Select(r => r.Requirement));

            foreach (var memoryItem in dumpAsset.MemoryAddresses.Where(m => !String.IsNullOrEmpty(m.FunctionName)))
            {
                var memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                var functionCall = memoryItem.FunctionName + "()";
                definition.Replace(memoryReference, functionCall);
            }

            stream.Write(definition.ToString());
        }
    }
}
