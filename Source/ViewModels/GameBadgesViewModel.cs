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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

            _achievements = new ObservableCollection<BadgeViewModel>();
            SearchCommand = new DelegateCommand(Search);
            ExportCommand = new DelegateCommand<ItemsControl>(Export);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly IFileSystemService _fileSystem;
        private readonly ISettings _settings;

        private string _gameName;

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
                    _gameName = publishedAssets.Title;
                    DialogTitle = "Game Badges - " + _gameName;

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

        public CommandBase<ItemsControl> ExportCommand { get; private set; }

        private void Export(ItemsControl listView)
        {
            var filename = _gameName + ".png";

            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export badges";
            vm.Filters["Image File"] = "*.png";
            vm.FileNames = new[] { filename };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                var rect = new Rect(new Point(), listView.RenderSize);
                DrawingVisual dv = new DrawingVisual();
                using (var context = dv.RenderOpen())
                {
                    var brush = new VisualBrush(listView);
                    context.DrawRectangle(brush, null, rect);
                }
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                    (int)rect.Width,
                    (int)rect.Height,
                    96, 96, // DPI
                    PixelFormats.Default);
                renderTargetBitmap.Render(dv);

                PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
                using (var fileStream = new FileStream(vm.FileNames[0], FileMode.Create))
                {
                    pngEncoder.Save(fileStream);
                }
            }
        }
    }
}
