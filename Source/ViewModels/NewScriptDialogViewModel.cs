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
            : this(ServiceRepository.Instance.FindService<ISettings>(),
                   ServiceRepository.Instance.FindService<IDialogService>(),
                   ServiceRepository.Instance.FindService<IFileSystemService>())
        {

        }

        internal NewScriptDialogViewModel(ISettings settings, IDialogService dialogService,
            IFileSystemService fileSystemService)
            : base(dialogService)
        {
            _settings = settings;
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
            _ticketNotes = new Dictionary<int, string>();
            _achievementSetVariables = new Dictionary<int, string>();

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

            NameStyles = new[]
            {
                new NameStyleLookupItem(NameStyle.None, "None"),
                new NameStyleLookupItem(NameStyle.SnakeCase, "snake_case"),
                new NameStyleLookupItem(NameStyle.CamelCase, "camelCase"),
                new NameStyleLookupItem(NameStyle.PascalCase, "PascalCase"),
            };

            MemoryAddresses = new GridViewModel();
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Size", MemoryItem.SizeProperty, new DelegateConverter(a => Field.GetSizeFunction((FieldSize)a), null)) { Width = 48 });
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Address", MemoryItem.AddressProperty, new DelegateConverter(a => String.Format("0x{0:X6}", a), null)) { Width = 56 });
            MemoryAddresses.Columns.Add(new TextColumnDefinition("Function Name", MemoryItem.FunctionNameProperty, new StringFieldMetadata("Function Name", 40, StringFieldAttributes.Required)) { Width = 120 });
            MemoryAddresses.Columns.Add(new DisplayTextColumnDefinition("Notes", MemoryItem.NotesProperty));
        }

        private readonly ISettings _settings;
        private readonly IFileSystemService _fileSystemService;

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

                    LoadMemoryItems();
                    UpdateMemoryGrid();

                    return;
                }
            }

            TaskDialogViewModel.ShowWarningMessage("Could not locate code notes for game " + gameId,
                "The game does not appear to have been recently loaded in any of the emulators specified in the Settings dialog.");
            return;
        }

        private void LoadGame(int gameId, string raCacheDirectory)
        {
            var filename = Path.Combine(raCacheDirectory, gameId + ".json");
            _publishedAssets = new PublishedAssets(filename, _fileSystemService);
            DialogTitle = "New Script - " + _publishedAssets.Title;

            _assets.Clear();
            _ticketNotes.Clear();
            _memoryItems.Clear();
            MemoryAddresses.Rows.Clear();

            _memoryAccessors = _publishedAssets.GetMemoryAccessors();

            var userFile = Path.Combine(raCacheDirectory, gameId + "-User.txt");
            _localAssets = new LocalAssets(userFile, _fileSystemService);

            LoadAchievements();
            LoadLeaderboards();
            LoadRichPresence();
            LoadNotes();

            if (_assets.Count == 0)
                SelectedCodeNotesFilter = CodeNoteFilter.All;

            IsGameLoaded = true;
            GameId.IsEnabled = false;

            if (_assets.Count > 0 && CanCheckWithTickets)
                ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(MergeOpenTickets);
        }

        private void LoadAchievements()
        {
            var unofficialAchievements = new List<DumpAsset>();
            foreach (var publishedAchievement in _publishedAssets.Achievements)
            {
                var dumpAchievement = new DumpAsset(publishedAchievement.Id, publishedAchievement.Title)
                {
                    Type = DumpAssetType.Achievement,
                    ViewerImage = "/RATools;component/Resources/achievement.png",
                    ViewerType = "Achievement",
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

                dumpAchievement.IsSelected = true;
                dumpAchievement.PropertyChanged += DumpAsset_PropertyChanged;
            }

            foreach (var unofficialAchievement in unofficialAchievements)
                _assets.Add(unofficialAchievement);

            foreach (var localAchievement in _localAssets.Achievements)
            {
                var dumpAchievement = _assets.FirstOrDefault(a => a.Id == localAchievement.Id && a.Type == DumpAssetType.Achievement);
                if (dumpAchievement != null)
                {
                    dumpAchievement.IsUnofficial = true;
                    dumpAchievement.ViewerType = "Local Achievement";
                }
                else
                {
                    dumpAchievement = new DumpAsset(localAchievement.Id, localAchievement.Title)
                    {
                        Type = DumpAssetType.Achievement,
                        ViewerImage = "/RATools;component/Resources/achievement.png",
                        ViewerType = "Local Achievement",
                        IsUnofficial = true
                    };

                    dumpAchievement.PropertyChanged += DumpAsset_PropertyChanged;
                    _assets.Add(dumpAchievement);
                }
            }
        }

        private void LoadLeaderboards()
        {
            foreach (var publishedLeaderboard in _publishedAssets.Leaderboards)
            {
                var dumpLeaderboard = new DumpAsset(publishedLeaderboard.Id, publishedLeaderboard.Title)
                {
                    Type = DumpAssetType.Leaderboard,
                    ViewerImage = "/RATools;component/Resources/leaderboard.png",
                    ViewerType = "Leaderboard",
                };

                _assets.Add(dumpLeaderboard);

                dumpLeaderboard.IsSelected = true;
                dumpLeaderboard.PropertyChanged += DumpAsset_PropertyChanged;
            }

            foreach (var localLeaderboard in _localAssets.Leaderboards)
            {
                var dumpLeaderboard = _assets.FirstOrDefault(a => a.Id == localLeaderboard.Id && a.Type == DumpAssetType.Leaderboard);
                if (dumpLeaderboard != null)
                {
                    dumpLeaderboard.IsUnofficial = true;
                    dumpLeaderboard.ViewerType = "Local Leaderboard";
                }
                else
                {
                    dumpLeaderboard = new DumpAsset(localLeaderboard.Id, localLeaderboard.Title)
                    {
                        Type = DumpAssetType.Achievement,
                        ViewerImage = "/RATools;component/Resources/leaderboard.png",
                        ViewerType = "Local Leaderboard",
                        IsUnofficial = true
                    };

                    dumpLeaderboard.PropertyChanged += DumpAsset_PropertyChanged;
                    _assets.Add(dumpLeaderboard);
                }
            }
        }

        private void LoadNotes()
        {
            _publishedAssets.LoadNotes();

            foreach (var note in _publishedAssets.Notes.Values)
            {
                var memoryAccessor = new MemoryAccessorAlias(note.Address, note);
                var index = _memoryAccessors.BinarySearch(memoryAccessor, memoryAccessor);

                if (index >= 0)
                    continue;

                var size = note.Size;
                switch (size)
                {
                    case FieldSize.None:
                    case FieldSize.Array:
                        // these sizes don't have accessor functions, fallback to byte()
                        size = FieldSize.Byte;
                        break;
                }

                memoryAccessor.ReferenceSize(size);
                _memoryAccessors.Insert(~index, memoryAccessor);
            }
        }

        private static int CompareMemoryItems(MemoryItem left, MemoryItem right)
        {
            var diff = (int)(left.Address - right.Address);
            if (diff == 0)
            {
                if (left.IsPrimarySize)
                    return right.IsPrimarySize ? 0 : -1;
                else if (right.IsPrimarySize)
                    return 1;

                if (diff == 0)
                    diff = (int)left.Size - (int)right.Size;
            }
            return diff;
        }

        private void LoadMemoryItems()
        {
            LoadMemoryItems(_memoryItems, _memoryAccessors, SelectedNameStyle);

            if (SelectedNameStyle != NameStyle.None)
                MemoryAccessorAlias.ResolveConflictingAliases(_memoryAccessors);

            foreach (var asset in _assets)
            {
                var memoryAccessors = new List<MemoryAccessorAlias>();

                switch (asset.Type)
                {
                    case DumpAssetType.Achievement:
                        var achievement = _localAssets?.Achievements.FirstOrDefault(a => a.Id == asset.Id);
                        if (achievement == null)
                            achievement = _publishedAssets.Achievements.FirstOrDefault(a => a.Id == asset.Id);

                        if (achievement != null)
                            MemoryAccessorAlias.AddMemoryAccessors(memoryAccessors, achievement, _publishedAssets.Notes);
                        break;

                    case DumpAssetType.Leaderboard:
                        var leaderboard = _localAssets?.Leaderboards.FirstOrDefault(l => l.Id == asset.Id);
                        if (leaderboard == null)
                            leaderboard = _publishedAssets.Leaderboards.FirstOrDefault(l => l.Id == asset.Id);

                        if (leaderboard != null)
                            MemoryAccessorAlias.AddMemoryAccessors(memoryAccessors, leaderboard, _publishedAssets.Notes);
                        break;

                    case DumpAssetType.RichPresence:
                        var richPresence = _localAssets?.RichPresence ?? _publishedAssets.RichPresence;
                        if (richPresence != null)
                            MemoryAccessorAlias.AddMemoryAccessors(memoryAccessors, richPresence, _publishedAssets.Notes);
                        break;

                    case DumpAssetType.Lookup:
                        var richPresence2 = _localAssets?.RichPresence ?? _publishedAssets.RichPresence;
                        if (richPresence2 != null)
                        {
                            foreach (var displayString in richPresence2.DisplayStrings)
                            {
                                foreach (var macro in displayString.Macros.Where(m => m.Name == asset.Label))
                                    MemoryAccessorAlias.AddMemoryAccessors(memoryAccessors, macro.Value, _publishedAssets.Notes);
                            }
                        }
                        break;

                }

                foreach (var memoryAccessor in memoryAccessors)
                {
                    foreach (var size in memoryAccessor.ReferencedSizes)
                    {
                        var memoryItem = _memoryItems.FirstOrDefault(m => m.Address == memoryAccessor.Address && m.Size == size);
                        if (memoryItem != null)
                            asset.MemoryAddresses.Add(memoryItem);
                    }
                }
            }
        }

        private static void LoadMemoryItems(List<MemoryItem> memoryItems,
            IEnumerable<MemoryAccessorAlias> memoryAccessors,
            NameStyle nameStyle)
        {
            foreach (var memoryAccessor in memoryAccessors)
            {
                memoryAccessor.UpdateAliasFromNote(nameStyle);

                var primarySize = memoryAccessor.PrimarySize;
                var memoryItem = new MemoryItem(memoryAccessor, primarySize);
                if (nameStyle != NameStyle.None)
                    memoryItem.UpdateFunctionName();

                memoryItems.Add(memoryItem);

                if (!memoryAccessor.IsOnlyReferencedSize(primarySize))
                {
                    foreach (var size in memoryAccessor.ReferencedSizes)
                    {
                        if (size != primarySize)
                        {
                            memoryItem = new MemoryItem(memoryAccessor, size);
                            if (nameStyle != NameStyle.None)
                                memoryItem.UpdateFunctionName();

                            memoryItems.Add(memoryItem);
                        }
                    }
                }

                if (memoryAccessor.Children.Any())
                {
                    LoadMemoryItems(memoryItem.ChainedItems, memoryAccessor.Children, nameStyle);
                    foreach (var child in memoryItem.ChainedItems)
                        child.Parent = memoryItem;
                }
            }

            memoryItems.Sort(CompareMemoryItems);
        }

        private void LoadRichPresence()
        {
            var richPresence = _localAssets?.RichPresence ?? _publishedAssets.RichPresence;
            if (richPresence == null)
                return;

            foreach (var macro in richPresence.Macros)
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

            if (richPresence.DisplayStrings.Any())
            {
                var dumpRichPresence = new DumpAsset(0, "Rich Presence Script")
                {
                    Type = DumpAssetType.RichPresence,
                    ViewerImage = "/RATools;component/Resources/rich_presence.png",
                    ViewerType = "Rich Presence"
                };

                dumpRichPresence.PropertyChanged += DumpAsset_PropertyChanged;
                _assets.Add(dumpRichPresence);
            }
        }

        private void MergeOpenTickets()
        {
            var ticketsJson = RAWebCache.Instance.GetOpenTicketsForGame(_publishedAssets.GameId);
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

        private PublishedAssets _publishedAssets;
        private LocalAssets _localAssets;
        private List<MemoryAccessorAlias> _memoryAccessors;
        private readonly Dictionary<int, string> _ticketNotes;
        
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

        public class NameStyleLookupItem
        {
            public NameStyleLookupItem(NameStyle id, string label)
            {
                Id = id;
                Label = label;
            }
            public NameStyle Id { get; private set; }
            public string Label { get; private set; }
        }
        public IEnumerable<NameStyleLookupItem> NameStyles { get; private set; }

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

        public static readonly ModelProperty SelectedNameStyleProperty = ModelProperty.Register(typeof(NewScriptDialogViewModel), "SelectedNameStyle", typeof(NameStyle), NameStyle.None, OnSelectedNameStyleChanged);
        public NameStyle SelectedNameStyle
        {
            get { return (NameStyle)GetValue(SelectedNameStyleProperty); }
            set { SetValue(SelectedNameStyleProperty, value); }
        }

        private static void OnSelectedNameStyleChanged(object sender, ModelPropertyChangedEventArgs e)
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

                foreach (var asset in _assets)
                {
                    if (!asset.IsSelected)
                        continue;

                    foreach (var address in asset.MemoryAddresses)
                    {
                        if (!visibleAddresses.Contains(address))
                            visibleAddresses.Add(address);
                    }
                }
            }

            // update the grid
            visibleAddresses.Sort(CompareMemoryItems);

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

                if (rowItem == null || rowItem.Address != memoryItem.Address || rowItem.Size != memoryItem.Size)
                    MemoryAddresses.InsertRow(memIndex, memoryItem);

                memIndex++;
            }

            while (memIndex < MemoryAddresses.Rows.Count)
                MemoryAddresses.Rows.RemoveAt(MemoryAddresses.Rows.Count - 1);
        }

        private void UpdateFunctionNames()
        {
            var nameStyle = SelectedNameStyle;
            foreach (var memoryAccessor in _memoryAccessors)
                memoryAccessor.UpdateAliasFromNote(nameStyle);

            if (nameStyle != NameStyle.None)
                MemoryAccessorAlias.ResolveConflictingAliases(_memoryAccessors);

            foreach (var row in MemoryAddresses.Rows)
            {
                var memoryItem = (MemoryItem)row.Model;
                memoryItem.UpdateFunctionName();
            }
        }

        [DebuggerDisplay("{Size} {Address,h}")]
        public class MemoryItem : ViewModelBase
        {
            public MemoryItem(MemoryAccessorAlias memoryAccessor, FieldSize size)
            {
                _memoryAccessor = memoryAccessor;

                Address = memoryAccessor.Address;
                Size = size;
                Notes = memoryAccessor.Note?.Note ?? String.Empty;
            }

            private readonly MemoryAccessorAlias _memoryAccessor;

            public bool IsPrimarySize
            {
                get { return _memoryAccessor.PrimarySize == Size; }
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

            public CodeNote Note
            {
                get { return _memoryAccessor.Note; }
            }

            public bool HasChainedItems
            {
                get { return _chainedItems != null; }
            }

            public List<MemoryItem> ChainedItems
            {
                get
                {
                    if (_chainedItems == null)
                        _chainedItems = new List<MemoryItem>();

                    return _chainedItems;
                }
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private List<MemoryItem> _chainedItems;

            public MemoryItem Parent { get; set; }
            public bool IsReferenced { get; set; }

            public void UpdateFunctionName()
            {
                FunctionName = _memoryAccessor.GetAlias(Size);

                if (_chainedItems != null)
                {
                    foreach (var child in _chainedItems)
                        child.UpdateFunctionName();
                }
            }
        }

        private readonly List<MemoryItem> _memoryItems;

        public GameViewModel Finalize()
        {
            var gameViewModel = new GameViewModel(_publishedAssets.GameId, _publishedAssets.Title);
            gameViewModel.AssociateRACacheDirectory(Path.GetDirectoryName(_publishedAssets.Filename));
            gameViewModel.InitializeForUI();

            var cleansed = _publishedAssets.Title ?? "Untitled";
            foreach (var c in Path.GetInvalidFileNameChars())
                cleansed = cleansed.Replace(c.ToString(), "");
            if (String.IsNullOrEmpty(cleansed))
                cleansed = _publishedAssets.GameId.ToString();
            gameViewModel.Script.Filename = cleansed + ".rascript";

            var memoryStream = new MemoryStream();
            Dump(memoryStream);
            gameViewModel.Script.SetContent(Encoding.UTF8.GetString(memoryStream.ToArray()));
            gameViewModel.Script.SetModified();

            return gameViewModel;
        }

        private static string EscapeString(string input)
        {
            return input.Replace("\"", "\\\"");
        }

        private uint GetMask()
        {
            var mask = 0xFFFFFFFF;

            switch (_publishedAssets.ConsoleId)
            {
                case 40: // Dreamcast
                case 78: // DSi
                case 2:  // N64
                case 12: // PlayStation
                    mask = 0x00FFFFFF;
                    break;

                case 16: // GameCube
                    // GameCube docs suggest masking with 0x1FFFFFFF (extra F).
                    // both work. check to see which the game is using.
                    mask = 0x01FFFFFF;
                    foreach (var achievement in _publishedAssets.Achievements)
                    {
                        foreach (var group in achievement.Trigger.Groups)
                        {
                            foreach (var requirement in group.Requirements)
                            {
                                if (requirement.Type == RequirementType.AddAddress &&
                                    requirement.Operator == RequirementOperator.BitwiseAnd &&
                                    requirement.Right.Type == FieldType.Value)
                                {
                                    if (requirement.Right.Value >= mask)
                                        return requirement.Right.Value;
                                }
                            }
                        }
                    }
                    break;

                case 21: // PlayStation2
                case 41: // PSP
                    mask = 0x01FFFFFF;
                    break;

                case 19: // WII
                    mask = 0x1FFFFFFF;
                    break;

                case 5:  // GBA
                    // This is technically wrong as there's two distinct maps required.
                    //  $03000000 -> $00000000 (via just doing a 24-bit read)
                    //  $02000000 -> $00008000 (via an offset)
                    // However, most developers who have implemented sets just do a 24-bit
                    // read _and_ use a +0x8000 offset when necessary. As these offsets are
                    // encoded in the code notes, we shouldn't provide an explicit offset here.
                    mask = 0x00FFFFFF;
                    break;
            }

            return mask;
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

            var mask = GetMask();

            AddAliases(scriptBuilderContext, _memoryItems, mask);
            foreach (var asset in _assets)
                AddAliases(scriptBuilderContext, asset.MemoryAddresses, mask);

            using (var stream = new StreamWriter(outStream))
            {
                stream.Write("// ");
                stream.WriteLine(_publishedAssets.Title);
                stream.Write("// #ID = ");
                stream.WriteLine(String.Format("{0}", _publishedAssets.GameId));

                if (_publishedAssets.Sets.Count() > 1)
                    DumpSets(stream, _publishedAssets.Sets);

                var lookupsToDump = _assets.Where(a => a.Type == DumpAssetType.Lookup && a.IsSelected).ToList();

                DumpMemoryAccessors(stream, lookupsToDump, scriptBuilderContext);

                foreach (var dumpLookup in lookupsToDump)
                    DumpLookup(stream, dumpLookup);

                DumpAchievements(stream, scriptBuilderContext);
                DumpLeaderboards(stream, scriptBuilderContext);
                DumpRichPresence(stream, scriptBuilderContext);
            }
        }

        private void AddAliases(ScriptBuilderContext scriptBuilderContext, IEnumerable<MemoryItem> items, uint mask)
        {
            foreach (var memoryItem in items.Where(m => !String.IsNullOrEmpty(m.FunctionName)))
            {
                var functionCall = memoryItem.FunctionName + "()";
                string memoryReference;

                if (memoryItem.Parent == null)
                {
                    memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
                }
                else
                {
                    var context = scriptBuilderContext.Clone();
                    var requirements = new List<Requirement>();
                    for (var parent = memoryItem.Parent; parent != null; parent = parent.Parent)
                    {
                        var requirement = new Requirement
                        {
                            Type = RequirementType.AddAddress,
                            Left = new Field { Type = FieldType.MemoryAddress, Size = parent.Size, Value = parent.Address },
                        };

                        if (mask != 0xFFFFFFFF)
                        {
                            if (mask == 0x00FFFFFF)
                            {
                                if (Field.GetByteSize(requirement.Left.Size) > 3)
                                    requirement.Left = requirement.Left.ChangeSize(FieldSize.TByte);
                            }
                            else
                            {
                                requirement.Operator = RequirementOperator.BitwiseAnd;
                                requirement.Right = new Field { Type = FieldType.Value, Size = FieldSize.None, Value = mask };
                            }
                        }

                        requirements.Add(requirement);
                    }

                    requirements.Reverse();

                    requirements.Add(new Requirement
                    {
                        Left = new Field { Type = FieldType.MemoryAddress, Size = memoryItem.Size, Value = memoryItem.Address }
                    });

                    var builder = new StringBuilder();
                    context.AppendRequirements(builder, requirements);
                    memoryReference = builder.ToString();
                }

                if (memoryReference != functionCall)
                {
                    var existing = scriptBuilderContext.GetAliasDefinition(functionCall);
                    if (existing != null && existing != memoryReference)
                    {
                        var updateItem = memoryItem;
                        var count = 1;

                        string suffixedFunctionName = memoryItem.FunctionName + "_";
                        if (memoryItem.Note != null && memoryItem.Note.Size == memoryItem.Size)
                        {
                            // this size matches the note size, give it the unsuffixed alias
                            scriptBuilderContext.AddAlias(memoryReference, functionCall);

                            memoryReference = existing;
                            suffixedFunctionName += existing.Substring(0, existing.IndexOf('('));

                            updateItem = FindAlternateMemoryItemByFunctionName(_memoryItems, memoryItem);
                        }
                        else
                        {
                            var suffix = Field.GetSizeFunction(memoryItem.Size);
                            if (!memoryItem.FunctionName.EndsWith(suffix))
                            {
                                suffixedFunctionName += suffix;
                            }
                            else
                            {
                                if (!Char.IsDigit(memoryItem.FunctionName.Last()))
                                    suffixedFunctionName = memoryItem.FunctionName;
                                count = 2;
                            }
                        }

                        do
                        {
                            updateItem.FunctionName = (count == 1) ? suffixedFunctionName : (suffixedFunctionName + count);
                            functionCall = updateItem.FunctionName + "()";

                            existing = scriptBuilderContext.GetAliasDefinition(functionCall);
                            if (existing == null || existing == memoryReference)
                                break;

                            count++;
                        } while (true);
                    }

                    scriptBuilderContext.AddAlias(memoryReference, functionCall);
                }

                if (memoryItem.HasChainedItems)
                    AddAliases(scriptBuilderContext, memoryItem.ChainedItems, mask);
            }
        }

        private static MemoryItem FindAlternateMemoryItemByFunctionName(List<MemoryItem> memoryItems, MemoryItem memoryItem)
        {
            foreach (var scan in memoryItems)
            {
                if (scan.FunctionName == memoryItem.FunctionName && !ReferenceEquals(scan, memoryItem))
                    return scan;
            }

            foreach (var scan in memoryItems)
            {
                if (scan.HasChainedItems)
                {
                    var child = FindAlternateMemoryItemByFunctionName(scan.ChainedItems, memoryItem);
                    if (child != null)
                        return child;
                }
            }

            return null;
        }

        private readonly Dictionary<int, string> _achievementSetVariables;

        private void DumpSets(StreamWriter stream, IEnumerable<AchievementSet> publishedSets)
        {
            stream.WriteLine();

            foreach (var set in publishedSets)
            {
                if (set.OwnerGameId != _publishedAssets.GameId)
                {
                    var name = "achievement set " + set.Title;
                    var variableName = SelectedNameStyle.BuildName(name);
                    _achievementSetVariables[set.Id] = variableName;

                    stream.Write(variableName);
                    stream.Write(" = achievement_set(\"");
                    stream.Write(EscapeString(set.Title));
                    stream.Write("\", type=\"");
                    switch (set.Type)
                    {
                        case AchievementSetType.Bonus:
                            stream.Write("BONUS");
                            break;
                        case AchievementSetType.Specialty:
                            stream.Write("SPECIALTY");
                            break;
                        case AchievementSetType.Exclusive:
                            stream.Write("EXCLUSIVE");
                            break;
                        default:
                            stream.Write(set.Type.ToString());
                            break;
                    }
                    stream.Write("\", id=");
                    stream.Write(set.Id);
                    stream.WriteLine(")");
                }
            }
        }

        private void DumpMemoryAccessors(StreamWriter stream, List<DumpAsset> lookupsToDump, ScriptBuilderContext scriptBuilderContext)
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
                if (dumpNotes != NoteDump.None)
                {
                    if (memoryItem.Note != null)
                    {
                        if (String.IsNullOrEmpty(memoryItem.FunctionName))
                        {
                            if (dumpNotes == NoteDump.OnlyForDefinedMethods)
                                continue;

                            if (memoryItem.Address == previousNoteAddress)
                                continue;
                        }
                    }

                    if (memoryItem.Note != null && memoryItem.Address != previousNoteAddress)
                    {
                        previousNoteAddress = memoryItem.Address;

                        var notes = memoryItem.Note.Note.Trim();
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

                if (filter == CodeNoteFilter.ForSelectedAssets)
                {
                    if (MemoryAddresses.GetRow(memoryItem) == null)
                        continue;
                }

                if (!String.IsNullOrEmpty(memoryItem.FunctionName))
                {
                    DumpMemoryFunction(stream, lookupsToDump, scriptBuilderContext, memoryItem, ref needLine);
                    hadFunction = true;
                }
                else
                {
                    hadFunction = false;
                }

                if (memoryItem.HasChainedItems)
                    hadFunction |= DumpNestedMemoryFunctions(stream, lookupsToDump, scriptBuilderContext, memoryItem, ref needLine);
            }
        }

        private bool DumpNestedMemoryFunctions(StreamWriter stream, List<DumpAsset> lookupsToDump, ScriptBuilderContext scriptBuilderContext, MemoryItem parent, ref bool needLine)
        {
            bool hadFunction = false;

            foreach (var memoryItem in parent.ChainedItems)
            {
                if (!String.IsNullOrEmpty(memoryItem.FunctionName))
                {
                    DumpMemoryFunction(stream, lookupsToDump, scriptBuilderContext, memoryItem, ref needLine);
                    hadFunction = true;
                }

                if (memoryItem.HasChainedItems)
                    hadFunction |= DumpNestedMemoryFunctions(stream, lookupsToDump, scriptBuilderContext, memoryItem, ref needLine);
            }

            return hadFunction;
        }

        private void DumpMemoryFunction(StreamWriter stream, List<DumpAsset> lookupsToDump, ScriptBuilderContext scriptBuilderContext, MemoryItem memoryItem, ref bool needLine)
        {
            string memoryReference;

            if (memoryItem.Parent == null)
            {
                memoryReference = Field.GetMemoryReference(memoryItem.Address, memoryItem.Size);
            }
            else
            {
                memoryReference = scriptBuilderContext.GetAliasDefinition(memoryItem.FunctionName + "()");
                if (memoryReference == null)
                    return;
            }

            if (needLine)
            {
                needLine = false;
                stream.WriteLine();
            }

            if (memoryItem.FunctionName.EndsWith("()"))
                memoryItem.FunctionName = memoryItem.FunctionName.Substring(0, memoryItem.FunctionName.Length - 2);

            stream.Write("function ");
            stream.Write(memoryItem.FunctionName);
            stream.Write("() => ");
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
                var achievement = _localAssets?.Achievements.FirstOrDefault(a => a.Id == dumpAchievement.Id);
                if (achievement == null)
                {
                    achievement = _publishedAssets.Achievements.FirstOrDefault(a => a.Id == dumpAchievement.Id);
                    if (achievement == null)
                        continue;
                }

                stream.WriteLine();

                DumpTickets(stream, dumpAchievement);

                stream.WriteLine("achievement(");

                stream.Write("    title = \"");
                stream.Write(EscapeString(achievement.Title));
                stream.Write("\", points = ");
                stream.Write(achievement.Points);
                if (achievement.Type != AchievementType.Standard)
                {
                    stream.Write(", type=\"");
                    stream.Write(Achievement.GetTypeString(achievement.Type));
                    stream.Write("\"");
                }
                stream.WriteLine(",");

                stream.Write("    description = \"");
                stream.Write(EscapeString(achievement.Description));
                stream.WriteLine("\",");

                string setVariable;
                if (_achievementSetVariables.TryGetValue(achievement.OwnerSetId, out setVariable))
                {
                    stream.Write("    set = ");
                    stream.Write(setVariable);
                    stream.WriteLine(",");
                }

                if (dumpAchievement.ViewerType != "Local Achievement")
                {
                    stream.Write("    id = ");
                    stream.Write(achievement.Id);
                    stream.Write(", badge = \"");
                    stream.Write(achievement.BadgeName);
                    stream.Write("\", published = \"");
                    stream.Write(achievement.Published);
                    stream.Write("\", modified = \"");
                    stream.Write(achievement.LastModified);
                    stream.WriteLine("\",");
                }

                stream.Write("    trigger = ");
                DumpTrigger(stream, indentedContext, dumpAchievement, achievement.Trigger);
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
                var leaderboard = _localAssets?.Leaderboards.FirstOrDefault(l => l.Id == dumpLeaderboard.Id);
                if (leaderboard == null)
                {
                    leaderboard = _publishedAssets.Leaderboards.FirstOrDefault(l => l.Id == dumpLeaderboard.Id);
                    if (leaderboard == null)
                        continue;
                }

                stream.WriteLine();

                DumpTickets(stream, dumpLeaderboard);

                stream.WriteLine("leaderboard(");

                stream.Write("    id = ");
                stream.Write(leaderboard.Id);
                stream.Write(", title = \"");
                stream.Write(EscapeString(leaderboard.Title));
                stream.WriteLine("\",");
                stream.Write("    description = \"");
                stream.Write(EscapeString(leaderboard.Description));
                stream.WriteLine("\",");

                string setVariable;
                if (_achievementSetVariables.TryGetValue(leaderboard.OwnerSetId, out setVariable))
                {
                    stream.Write("    set = ");
                    stream.Write(setVariable);
                    stream.WriteLine(",");
                }

                stream.Write("    start  = ");
                DumpTrigger(stream, indentedContext, dumpLeaderboard, leaderboard.Start);
                stream.WriteLine(",");

                stream.Write("    cancel = ");
                DumpTrigger(stream, indentedContext, dumpLeaderboard, leaderboard.Cancel);
                stream.WriteLine(",");

                stream.Write("    submit = ");
                DumpTrigger(stream, indentedContext, dumpLeaderboard, leaderboard.Submit);
                stream.WriteLine(",");

                stream.Write("    value  = ");
                var valueTrigger = leaderboard.Value;
                if (valueTrigger.Values.Count() > 1 ||
                    valueTrigger.Values.First().Requirements.Any(r => r.IsMeasured))
                {
                    DumpValue(stream, indentedContext, dumpLeaderboard, valueTrigger);
                }
                else
                {
                    DumpLegacyExpression(stream, valueTrigger, dumpLeaderboard, indentedContext);
                }
                stream.WriteLine(",");

                stream.Write("    format = \"");
                stream.Write(Leaderboard.GetFormatString(leaderboard.Format));
                stream.Write("\"");

                if (leaderboard.LowerIsBetter)
                    stream.Write(", lower_is_better = true");

                stream.WriteLine();
                stream.WriteLine(")");
            }
        }

        private void DumpLookup(StreamWriter stream, DumpAsset dumpLookup)
        {
            var richPresence = _localAssets?.RichPresence ?? _publishedAssets?.RichPresence;
            var macro = richPresence?.Macros.FirstOrDefault(m => m.Name == dumpLookup.Label);
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

        private void DumpRichPresence(StreamWriter stream, ScriptBuilderContext scriptBuilderContext)
        {
            var dumpRichPresence = _assets.FirstOrDefault(a => a.Type == DumpAssetType.RichPresence);
            if (dumpRichPresence == null || !dumpRichPresence.IsSelected)
                return;

            var richPresence = _localAssets?.RichPresence ?? _publishedAssets.RichPresence;
            if (richPresence == null)
                return;

            var notes = new Dictionary<uint, CodeNote>();

            var indentedContext = scriptBuilderContext.Clone();
            indentedContext.Indent = 4;

            foreach (var displayString in richPresence.DisplayStrings)
            {
                stream.WriteLine();

                if (displayString.Condition != null)
                {
                    stream.Write("rich_presence_conditional_display(");
                    indentedContext.Indent = 4;
                    DumpTrigger(stream, indentedContext, dumpRichPresence, displayString.Condition);
                    stream.Write(", \"");
                }
                else
                {
                    stream.Write("rich_presence_display(\"");
                }

                int index = 0;
                int macroCount = 0;
                do
                {
                    var index1 = displayString.Text.IndexOf('@', index);
                    if (index1 == -1)
                    {
                        stream.Write(displayString.Text.Substring(index));
                        break;
                    }

                    if (index1 > index)
                        stream.Write(displayString.Text.Substring(index, index1 - index));

                    stream.Write('{');
                    stream.Write(macroCount++);
                    stream.Write('}');

                    var rightParenIndex = displayString.Text.IndexOf(')', index1);
                    index = rightParenIndex + 1;
                } while (true);

                stream.Write('"');

                foreach (var macro in displayString.Macros)
                {
                    stream.WriteLine(",");
                    stream.Write("    ");

                    var macroDefinition = richPresence.Macros.FirstOrDefault(m => m.Name == macro.Name);
                    if (macroDefinition == null)
                    {
                        stream.Write("rich_presence_value(\"");
                        stream.Write(macro.Name);
                        stream.Write("\", ");
                        indentedContext.Indent = 24; // "    rich_presence_value(".length
                    }
                    else if (macroDefinition.LookupEntries != null)
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

                    if (macro.Value.Values.Any())
                    {
                        var measured = macro.Value.Values.First().Requirements.FirstOrDefault(r => r.Type == RequirementType.Measured);
                        if (measured != null)
                            measured.Type = RequirementType.None; // measured() is implicit
                    }
                    DumpLegacyExpression(stream, macro.Value, dumpRichPresence, indentedContext);

                    if (macroDefinition == null)
                    {
                        var macroFormat = RichPresenceBuilder.GetValueFormat(macro.Name);
                        if (macroFormat != ValueFormat.None && macroFormat != ValueFormat.Value)
                        {
                            stream.Write(", format=\"");
                            stream.Write(RichPresenceBuilder.GetFormatString(macroFormat));
                            stream.Write('\"');
                        }

                        stream.Write(')');
                    }
                    else if (macroDefinition.LookupEntries != null)
                    { 
                        stream.Write(", ");
                        stream.Write(macro.Name);
                        stream.Write("Lookup");

                        string defaultEntry;
                        if (macroDefinition.LookupEntries.TryGetValue("*", out defaultEntry))
                        {
                            stream.Write(", fallback=\"");
                            stream.Write(EscapeString(defaultEntry));
                            stream.Write('"');
                        }    

                        stream.Write(')');
                    }
                    else
                    {
                        if (macroDefinition.FormatType != ValueFormat.Value)
                        {
                            stream.Write(", format=\"");
                            stream.Write(Leaderboard.GetFormatString(macroDefinition.FormatType));
                            stream.Write('"');
                        }

                        stream.Write(')');
                    }
                }

                if (displayString.Macros.Any())
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

        private static void DumpTrigger(StreamWriter stream, ScriptBuilderContext scriptBuilderContext, DumpAsset dumpAsset, Trigger trigger)
        {
            var triggerWhenMeasuredGroups = new List<RequirementGroup>();
            var triggerGroupCount = trigger.Groups.Count();
            if (triggerGroupCount > 2)
                IdentifyTriggerWhenMeasured(trigger, triggerWhenMeasuredGroups);

            var groupEnumerator = trigger.Groups.GetEnumerator();
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

                    if (triggerGroupCount == 2)
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

        private static void IdentifyTriggerWhenMeasured(Trigger trigger, List<RequirementGroup> triggerWhenMeasuredGroups)
        {
            RequirementEx triggerAlt = null;
            foreach (var group in trigger.Alts)
            {
                if (!group.Requirements.Any(r => r.Type == RequirementType.Trigger))
                    continue;

                var groupEx = RequirementEx.Combine(group.Requirements);
                if (groupEx.Count == 1)
                {
                    triggerWhenMeasuredGroups.Add(group);
                    triggerAlt = groupEx[0];
                    break;
                }
            }

            if (triggerAlt == null)
                return;

            foreach (var group in trigger.Alts)
            {
                if (!group.Requirements.Any(r => r.IsMeasured))
                    continue;

                var groupEx = RequirementEx.Combine(group.Requirements);
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

        private static void DumpValue(StreamWriter stream, ScriptBuilderContext context, DumpAsset dumpAsset, Value value)
        {
            if (value.Values.Count() > 1)
            {
                stream.WriteLine("max_of(");
                context.Indent += 4;

                bool first = true;
                foreach (var scan in value.Values)
                {
                    if (!first)
                        stream.WriteLine(",");

                    stream.Write(new string(' ', context.Indent));
                    first = false;

                    DumpPublishedRequirements(stream, dumpAsset, scan, context, true);
                }

                context.Indent -= 4;
                stream.WriteLine();
                stream.Write(new string(' ', context.Indent));
                stream.Write(")");
            }
            else
            {
                DumpPublishedRequirements(stream, dumpAsset, value.Values.First(), context, true);
            }
        }

        private static void DumpPublishedRequirements(StreamWriter stream, DumpAsset dumpAsset,
            RequirementGroup requirementGroup, ScriptBuilderContext scriptBuilderContext, bool isValue = false)
        {
            var definition = new StringBuilder();
            var context = scriptBuilderContext.Clone();
            context.IsValue = isValue;
            context.AppendRequirements(definition, requirementGroup.Requirements);

            stream.Write(definition.ToString());
        }
    }
}
