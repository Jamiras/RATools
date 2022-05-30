//#define ONLY_WITH_HASHES

using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.ViewModels
{
    public class GameDataSnapshotViewModel : ViewModelBase
    {
        public GameDataSnapshotViewModel(ProgressFieldViewModel progress)
            : this(progress, ServiceRepository.Instance.FindService<IBackgroundWorkerService>(), ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public GameDataSnapshotViewModel(ProgressFieldViewModel progress, IBackgroundWorkerService backgroundWorkerService, ISettings settings)
        {
            _progress = progress;
            _backgroundWorkerService = backgroundWorkerService;
            _settings = settings;

            if (String.IsNullOrEmpty(settings.DoRequestToken))
                RefreshCommand = DisabledCommand.Instance;
            else
                RefreshCommand = new DelegateCommand(DoRefresh);

            _gamesWithAchievements = new List<int>();
            _gamesWithLeaderboards = new List<int>();
            _gamesWithRichPresence = new List<int>();

            if (_progress != null)
                _backgroundWorkerService.RunAsync(LoadFromDisk);
        }

        private readonly ProgressFieldViewModel _progress;

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly ISettings _settings;

        public IEnumerable<int> GamesWithAchievements
        {
            get { return _gamesWithAchievements; }
        }
        private readonly List<int> _gamesWithAchievements;

        public IEnumerable<int> GamesWithRichPresence
        {
            get { return _gamesWithRichPresence; }
        }
        private readonly List<int> _gamesWithRichPresence;

        public IEnumerable<int> GamesWithLeaderboards
        {
            get { return _gamesWithLeaderboards; }
        }
        private readonly List<int> _gamesWithLeaderboards;

        public CommandBase RefreshCommand { get; private set; }
        private void DoRefresh()
        {
            _backgroundWorkerService.RunAsync(() => RefreshFromServer());
        }

        internal void RefreshFromServer()
        {
            var rand = new Random();
#if ONLY_WITH_HASHES
            var ids = new List<int>();
#else
            var minId = 1;
            var maxId = 0;
#endif

            var fileSystemService = ServiceRepository.Instance.FindService<IFileSystemService>();
            var httpRequestService = ServiceRepository.Instance.FindService<IHttpRequestService>();

            if (_progress != null)
            {
                _progress.IsEnabled = true;
                _progress.Reset(1000);
                _progress.Label = "Fetching data...";
            }

            // fetch all games that have hashes associated to them (can't earn achievements without a hash)
            {
                var json = RAWebCache.Instance.GetAllHashes();
                var hashes = json.GetField("MD5List").ObjectValue;
                foreach (var pair in hashes)
                {
#if ONLY_WITH_HASHES
                    if (!ids.Contains(pair.IntegerValue.GetValueOrDefault()))
                        ids.Add(pair.IntegerValue.GetValueOrDefault());
#else
                    if (pair.IntegerValue.GetValueOrDefault() > maxId)
                        maxId = pair.IntegerValue.GetValueOrDefault();
#endif
                }
            }

#if ONLY_WITH_HASHES
            ids.Sort();

            if (_progress != null)
                _progress.Reset(ids.Count);

            foreach (var gameId in ids)
#else
            if (_progress != null)
                _progress.Reset(maxId - minId + 1);

            for (var gameId = minId; gameId <= maxId; ++gameId)
#endif
            {
                if (_progress != null)
                {
                    if (!_progress.IsEnabled) // error encountered
                        return;

                    _progress.Current++;
                }

                RefreshFromServer(gameId, fileSystemService, httpRequestService);

                System.Threading.Thread.Sleep(rand.Next(300) + 100);
            }

            // download completed, refresh the stats
            if (_progress != null)
                LoadFromDisk();
        }

        private void RefreshFromServer(int gameId, IFileSystemService fileSystemService, IHttpRequestService httpRequestService)
        {
            var file = Path.Combine(_settings.DumpDirectory, String.Format("{0}.json", gameId));
            if (fileSystemService.FileExists(file))
            {
                bool fileValid = (DateTime.Now - fileSystemService.GetFileLastModified(file)) < TimeSpan.FromHours(16);
                if (fileValid)
                    return;
            }

            Debug.WriteLine(String.Format("{0} fetching patch data {1}", DateTime.Now, gameId));
            var url = String.Format("https://retroachievements.org/dorequest.php?u={0}&t={1}&g={2}&h=1&r=patch", 
                _settings.UserName, _settings.DoRequestToken, gameId);
            var request = new HttpRequest(url);
            var response = httpRequestService.Request(request);
            if (response.Status == System.Net.HttpStatusCode.OK)
            {
                using (var outputStream = fileSystemService.CreateFile(file))
                {
                    byte[] buffer = new byte[4096];
                    using (var stream = response.GetResponseStream())
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (bytesRead < 100)
                            {
                                var str = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                if (str.Contains("Error"))
                                {
                                    if (_progress != null)
                                    {
                                        _progress.IsEnabled = false;
                                        _progress.Label = String.Empty;
                                    }
                                    _backgroundWorkerService.InvokeOnUiThread(() => MessageBoxViewModel.ShowMessage(str));
                                    return;
                                }
                            }
                            outputStream.Write(buffer, 0, bytesRead);
                        }
                    }
                }

                string contents;
                using (var stream = new StreamReader(fileSystemService.OpenFile(file, OpenFileMode.Read)))
                {
                    contents = stream.ReadToEnd();
                }
            }
        }

        private void LoadFromDisk()
        {
            int gameCount = 0;
            int staticRichPresenceCount = 0;
            int leaderboardCount = 0;
            int achievementCount = 0;
            var oldestFile = DateTime.MaxValue;
            var authors = new HashSet<string>();
            var systems = new HashSet<int>();

            _gamesWithAchievements.Clear();
            _gamesWithLeaderboards.Clear();
            _gamesWithRichPresence.Clear();

            var directory = _settings.DumpDirectory;
            var files = Directory.GetFiles(directory, "*.json");

            _progress.Reset(files.Length);
            _progress.Label = "Processing cached files";
            _progress.IsEnabled = true;

            foreach (var file in files)
            {
                if (!_progress.IsEnabled)
                    break;

                _progress.Current++;

                var contents = File.ReadAllText(file);
                var json = new Jamiras.IO.Serialization.JsonObject(contents);
                var patchData = json.GetField("PatchData");
                if (patchData.Type != Jamiras.IO.Serialization.JsonFieldType.Object)
                {
                    File.Delete(file);
                    continue;
                }

                int gameId = patchData.ObjectValue.GetField("ID").IntegerValue.GetValueOrDefault();
                if (gameId == 0)
                {
                    File.Delete(file);
                    continue;
                }

                var lastUpdated = File.GetLastWriteTimeUtc(file);
                if (lastUpdated < oldestFile)
                    oldestFile = lastUpdated;

                gameCount++;

                var richPresence = patchData.ObjectValue.GetField("RichPresencePatch").StringValue;
                if (richPresence != null)
                {
                    int index = richPresence.IndexOf("Display:");
                    if (index != -1)
                    {
                        _gamesWithRichPresence.Add(gameId);

                        bool dynamic = false;
                        foreach (var line in richPresence.Substring(index).Split('\n'))
                        {
                            if (line.Trim().Length == 0)
                                break;

                            if (line.Contains("@"))
                            {
                                dynamic = true;
                                break;
                            }
                        }

                        if (!dynamic)
                            staticRichPresenceCount++;
                    }
                }

                var leaderboards = patchData.ObjectValue.GetField("Leaderboards").ObjectArrayValue;
                if (leaderboards != null && leaderboards.Any())
                {
                    _gamesWithLeaderboards.Add(gameId);
                    leaderboardCount += leaderboards.Count();
                }

                var achievements = patchData.ObjectValue.GetField("Achievements").ObjectArrayValue;
                if (achievements != null && achievements.Any())
                {
                    _gamesWithAchievements.Add(gameId);
                    achievementCount += achievements.Count();

                    foreach (var achievement in achievements)
                        authors.Add(achievement.GetField("Author").StringValue);
                }

                systems.Add(patchData.ObjectValue.GetField("ConsoleID").IntegerValue.GetValueOrDefault());
            }

            SetValue(GameCountProperty, gameCount);
            SetValue(AchievementCountProperty, achievementCount);
            SetValue(AchievementGameCountProperty, _gamesWithAchievements.Count);
            SetValue(LeaderboardCountProperty, leaderboardCount);
            SetValue(LeaderboardGameCountProperty, _gamesWithLeaderboards.Count);
            SetValue(RichPresenceCountProperty, _gamesWithRichPresence.Count);
            SetValue(StaticRichPresenceCountProperty, staticRichPresenceCount);
            SetValue(AuthorCountProperty, authors.Count);
            SetValue(SystemCountProperty, systems.Count);

            SetValue(LastUpdatedTextProperty, (oldestFile != DateTime.MaxValue) ? oldestFile.ToLocalTime().ToString() : LastUpdatedTextProperty.DefaultValue);

            _progress.IsEnabled = false;
            _progress.Label = String.Empty;

            if (DataRefreshed != null)
                DataRefreshed(this, EventArgs.Empty);
        }

        public event EventHandler DataRefreshed;

        public static readonly ModelProperty GameCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "GameCount", typeof(int), 0);
        public int GameCount
        {
            get { return (int)GetValue(GameCountProperty); }
        }

        public static readonly ModelProperty AchievementCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "AchievementCount", typeof(int), 0);
        public int AchievementCount
        {
            get { return (int)GetValue(AchievementCountProperty); }
        }

        public static readonly ModelProperty AchievementGameCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "AchievementGameCount", typeof(int), 0);
        public int AchievementGameCount
        {
            get { return (int)GetValue(AchievementGameCountProperty); }
        }

        public static readonly ModelProperty LeaderboardCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "LeaderboardCount", typeof(int), 0);
        public int LeaderboardCount
        {
            get { return (int)GetValue(LeaderboardCountProperty); }
        }

        public static readonly ModelProperty LeaderboardGameCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "LeaderboardGameCount", typeof(int), 0);
        public int LeaderboardGameCount
        {
            get { return (int)GetValue(LeaderboardGameCountProperty); }
        }

        public static readonly ModelProperty RichPresenceCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "RichPresenceCount", typeof(int), 0);
        public int RichPresenceCount
        {
            get { return (int)GetValue(RichPresenceCountProperty); }
        }

        public static readonly ModelProperty StaticRichPresenceCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "StaticRichPresenceCount", typeof(int), 0);
        public int StaticRichPresenceCount
        {
            get { return (int)GetValue(StaticRichPresenceCountProperty); }
        }

        public static readonly ModelProperty AuthorCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "AuthorCount", typeof(int), 0);
        public int AuthorCount
        {
            get { return (int)GetValue(AuthorCountProperty); }
        }

        public static readonly ModelProperty SystemCountProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "SystemCount", typeof(int), 0);
        public int SystemCount
        {
            get { return (int)GetValue(SystemCountProperty); }
        }

        public static readonly ModelProperty LastUpdatedTextProperty = ModelProperty.Register(typeof(GameDataSnapshotViewModel), "LastUpdatedText", typeof(string), "Never");
        public string LastUpdatedText
        {
            get { return (string)GetValue(LastUpdatedTextProperty); }
        }
    }
}
