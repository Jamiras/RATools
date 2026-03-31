using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Parser;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static RATools.ViewModels.GameStatsViewModel;

namespace RATools.ViewModels
{
    public class GameBadgesViewModel : DialogViewModelBase
    {
        public GameBadgesViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>(),
                   ServiceRepository.Instance.FindService<IFileSystemService>(),
                   ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public GameBadgesViewModel(IBackgroundWorkerService backgroundWorkerService, IFileSystemService fileSystem, ISettings settings)
        {
            _backgroundWorkerService = backgroundWorkerService;
            _fileSystem = fileSystem;
            _settings = settings;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Game Badges";
            CanClose = true;
            CancelButtonText = null;
            ExtraButtonCommand = new DelegateCommand(Export);

            _achievements = new ObservableCollection<BadgeViewModel>();
            SearchCommand = new DelegateCommand(Search);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly IFileSystemService _fileSystem;
        private readonly ISettings _settings;

        public ProgressFieldViewModel Progress { get; private set; }

        public static readonly ModelProperty GameIdProperty = ModelProperty.Register(typeof(GameStatsViewModel), "GameId", typeof(int), 0);

        public int GameId
        {
            get { return (int)GetValue(GameIdProperty); }
            set { SetValue(GameIdProperty, value); }
        }

        public class BadgeViewModel
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public ImageSource Badge { get; set; }
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            _achievements.Clear();

            int gameId = GameId;
            foreach (var directory in _settings.EmulatorDirectories)
            {
                var dataDirectory = Path.Combine(directory, "RACache", "Data");

                var filename = Path.Combine(dataDirectory, gameId + ".json");
                if (File.Exists(filename))
                {
                    var publishedAssets = new PublishedAssets(filename, _fileSystem);
                    DialogTitle = "Game Badges - " + publishedAssets.Title;

                    var badgeDirectory = Path.Combine(directory, "RACache", "Badge");
                    foreach (var achievement in publishedAssets.Achievements)
                    {
                        if (achievement.IsUnofficial)
                            continue;

                        var path = Path.Combine(badgeDirectory, achievement.BadgeName);
                        if (Path.GetExtension(path) == "")
                            path += ".png";

                        BitmapImage image = null;
                        if (File.Exists(path))
                        {
                            image = new BitmapImage(new Uri(path));
                            image.Freeze();
                        }

                        var badge = new BadgeViewModel
                        {
                            Title = achievement.Title,
                            Description = achievement.Description,
                            Badge = image,
                        };
                        _achievements.Add(badge);
                    }

                    return;
                }
            }

            Progress.Label = "Fetching Game " + GameId;
            Progress.IsEnabled = true;
            _backgroundWorkerService.RunAsync(() =>
            {
                LoadGame();
            });
        }

        internal void LoadGame()
        {
            int gameId = GameId;


            Progress.Label = String.Empty;
        }

        /// <summary>
        /// Gets the list of achievements.
        /// </summary>
        public IEnumerable<BadgeViewModel> Achievements { get {  return _achievements; } }
        private ObservableCollection<BadgeViewModel> _achievements;

        public static readonly ModelProperty ShowHardcoreBorderProperty = ModelProperty.Register(typeof(GameStatsViewModel), "GameId", typeof(bool), false);

        public bool ShowHardcoreBorder
        {
            get { return (bool)GetValue(ShowHardcoreBorderProperty); }
            set { SetValue(ShowHardcoreBorderProperty, value); }
        }

        public string ExtraButtonText {  get { return "Export"; } }
        public CommandBase ExtraButtonCommand { get; private set; }

        private void Export()
        {
            var filename = "achievements";

            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export achievement information";
            vm.Filters["CSV file"] = "*.csv";
            vm.FileNames = new[] { filename + ".csv" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                using (var file = File.CreateText(vm.FileNames[0]))
                {

                }
            }
        }
    }
}
