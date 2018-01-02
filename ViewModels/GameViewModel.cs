using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RATools.ViewModels
{
    [DebuggerDisplay("GameTitle")]
    public class GameViewModel : ViewModelBase
    {
        public GameViewModel(AchievementScriptInterpreter parser, string raCacheDirectory)
        {
            GameId = parser.GameId;
            Title = parser.GameTitle;
            RACacheDirectory = raCacheDirectory;

            Notes = new TinyDictionary<int, string>();
            using (var notesStream = File.OpenRead(Path.Combine(raCacheDirectory, parser.GameId + "-Notes2.txt")))
            {
                var reader = new StreamReader(notesStream);
                var firstChar = reader.Peek();
                notesStream.Seek(0, SeekOrigin.Begin);

                if (firstChar == '{')
                {
                    _isN64 = false;

                    // standard JSON format
                    var notes = new JsonObject(notesStream);
                    foreach (var note in notes.GetField("CodeNotes").ObjectArrayValue)
                    {
                        var address = Int32.Parse(note.GetField("Address").StringValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
                        var text = note.GetField("Note").StringValue;
                        Notes[address] = text;
                    }
                }
                else
                {
                    _isN64 = true;

                    // N64 unique format
                    var tokenizer = Tokenizer.CreateTokenizer(notesStream);
                    do
                    {
                        var unused = tokenizer.ReadTo(':');
                        if (tokenizer.NextChar == '\0')
                            break;
                        tokenizer.Advance();

                        int address;
                        if (tokenizer.Match("0x"))
                            address = Int32.Parse(tokenizer.ReadTo(':').ToString(), System.Globalization.NumberStyles.HexNumber);
                        else
                            address = Int32.Parse(tokenizer.ReadTo(':').ToString());
                        tokenizer.Advance();

                        var text = tokenizer.ReadTo('#');
                        tokenizer.Advance();

                        Notes[address] = text.ToString();
                    } while (true);
                }
            }

            _logger = ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools");
            _logger.WriteVerbose("Read " + Notes.Count + " code notes");

            var achievements = new List<GeneratedItemViewModelBase>(parser.Achievements.Count());
            foreach (var achievement in parser.Achievements)
            {
                var achievementViewModel = new GeneratedAchievementViewModel(this, achievement);
                achievements.Add(achievementViewModel);
            }

            if (_isN64)
                MergePublishedN64(parser.GameId, achievements);
            else
                MergePublished(parser.GameId, achievements);

            MergeLocal(parser.GameId, achievements);

            if (!String.IsNullOrEmpty(parser.RichPresence) && parser.RichPresence.Length > 8)
                achievements.Add(new RichPresenceViewModel(this, parser.RichPresence));

            foreach (var leaderboard in parser.Leaderboards)
                achievements.Add(new LeaderboardViewModel(this, leaderboard));

            foreach (var achievement in achievements.OfType<GeneratedAchievementViewModel>())
                achievement.UpdateCommonProperties(this);

            Achievements = achievements;
        }

        public GameViewModel(int gameId, string title)
        {
            GameId = gameId;
            Title = title;
        }

        private LocalAchievements _localAchievements;
        private readonly bool _isN64;
        private readonly ILogger _logger;

        internal int GameId { get; private set; }
        internal string RACacheDirectory { get; private set; }
        internal TinyDictionary<int, string> Notes { get; private set; }
        
        public static readonly ModelProperty AchievementsProperty = ModelProperty.Register(typeof(GameViewModel), "Achievements", typeof(IEnumerable<GeneratedItemViewModelBase>), null);
        public IEnumerable<GeneratedItemViewModelBase> Achievements
        {
            get { return (IEnumerable<GeneratedItemViewModelBase>)GetValue(AchievementsProperty); }
            private set { SetValue(AchievementsProperty, value); }
        }

        public static readonly ModelProperty SelectedAchievementProperty = ModelProperty.Register(typeof(GameViewModel), "SelectedAchievement", typeof(GeneratedItemViewModelBase), null);
        public GeneratedItemViewModelBase SelectedAchievement
        {
            get { return (GeneratedItemViewModelBase)GetValue(SelectedAchievementProperty); }
            set { SetValue(SelectedAchievementProperty, value); }
        }

        internal void UpdateLocal(Achievement achievement, Achievement localAchievement)
        {
            if (achievement == null)
            {
                _logger.WriteVerbose(String.Format("Deleting {0} from local achievements", localAchievement.Title));

                var previous = _localAchievements.Replace(localAchievement, null);
                if (previous != null && previous.Points != 0)
                    LocalAchievementPoints -= previous.Points;

                LocalAchievementCount--;
            }
            else
            {
                if (localAchievement != null)
                    _logger.WriteVerbose(String.Format("Updating {0} in local achievements", achievement.Title));
                else
                    _logger.WriteVerbose(String.Format("Committing {0} to local achievements", achievement.Title));

                var previous = _localAchievements.Replace(localAchievement, achievement);
                if (previous != null)
                {
                    var diff = achievement.Points - previous.Points;
                    if (diff != 0)
                        LocalAchievementPoints += diff;
                }
                else
                {
                    LocalAchievementCount++;
                    LocalAchievementPoints += achievement.Points;
                }
            }

            _localAchievements.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName);
        }

        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(MainWindowViewModel), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            private set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty PublishedAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "PublishedAchievementCount", typeof(int), 0);
        public int PublishedAchievementCount
        {
            get { return (int)GetValue(PublishedAchievementCountProperty); }
            private set { SetValue(PublishedAchievementCountProperty, value); }
        }

        public static readonly ModelProperty PublishedAchievementPointsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "PublishedAchievementPoints", typeof(int), 0);
        public int PublishedAchievementPoints
        {
            get { return (int)GetValue(PublishedAchievementPointsProperty); }
            private set { SetValue(PublishedAchievementPointsProperty, value); }
        }

        public static readonly ModelProperty LocalAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "LocalAchievementCount", typeof(int), 0);
        public int LocalAchievementCount
        {
            get { return (int)GetValue(LocalAchievementCountProperty); }
            private set { SetValue(LocalAchievementCountProperty, value); }
        }

        public static readonly ModelProperty LocalAchievementPointsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "LocalAchievementPoints", typeof(int), 0);
        public int LocalAchievementPoints
        {
            get { return (int)GetValue(LocalAchievementPointsProperty); }
            private set { SetValue(LocalAchievementPointsProperty, value); }
        }

        private static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private void MergePublished(int gameId, List<GeneratedItemViewModelBase> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + ".txt");
            if (!File.Exists(fileName))
                return;

            using (var stream = File.OpenRead(fileName))
            {
                var publishedData = new JsonObject(stream);
                Title = publishedData.GetField("Title").StringValue;

                var publishedAchievements = publishedData.GetField("Achievements");
                var count = 0;
                var points = 0;
                foreach (var publishedAchievement in publishedAchievements.ObjectArrayValue)
                {
                    count++;
                    points += publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();

                    var title = publishedAchievement.GetField("Title").StringValue;

                    var id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => a.Generated.Id == id);
                    if (achievement == null)
                    {
                        achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, title, StringComparison.CurrentCultureIgnoreCase) == 0);
                        if (achievement == null)
                        {
                            achievement = new GeneratedAchievementViewModel(this, null);
                            achievements.Add(achievement);
                        }
                    }

                    var builder = new AchievementBuilder();
                    builder.Id = id;
                    builder.Title = title;
                    builder.Description = publishedAchievement.GetField("Description").StringValue;
                    builder.Points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                    builder.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;
                    builder.ParseRequirements(Tokenizer.CreateTokenizer(publishedAchievement.GetField("MemAddr").StringValue));

                    var builtAchievement = builder.ToAchievement();
                    builtAchievement.Published = UnixEpoch.AddSeconds(publishedAchievement.GetField("Created").IntegerValue.GetValueOrDefault());
                    builtAchievement.LastModified = UnixEpoch.AddSeconds(publishedAchievement.GetField("Modified").IntegerValue.GetValueOrDefault());

                    if (publishedAchievement.GetField("Flags").IntegerValue == 5)
                        achievement.Unofficial.LoadAchievement(builtAchievement);
                    else
                        achievement.Core.LoadAchievement(builtAchievement);
                }

                PublishedAchievementCount = count;
                PublishedAchievementPoints = points;

                _logger.WriteVerbose(String.Format("Merged {0} published achievements ({1} points)", count, points));
            }
        }

        private void MergePublishedN64(int gameId, List<GeneratedItemViewModelBase> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + ".txt");
            if (!File.Exists(fileName))
                return;

            var count = 0;
            var points = 0;

            var officialAchievements = new LocalAchievements(fileName);
            foreach (var publishedAchievement in officialAchievements.Achievements)
            {
                var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, publishedAchievement.Title, StringComparison.CurrentCultureIgnoreCase) == 0);
                if (achievement == null)
                {
                    achievement = new GeneratedAchievementViewModel(this, null);
                    achievements.Add(achievement);
                }

                achievement.Core.LoadAchievement(publishedAchievement);
                count++;
                points += publishedAchievement.Points;
            }

            fileName = Path.Combine(RACacheDirectory, gameId + "-Unofficial.txt");
            if (File.Exists(fileName))
            {
                var unofficialAchievements = new LocalAchievements(fileName);
                foreach (var publishedAchievement in unofficialAchievements.Achievements)
                {
                    var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, publishedAchievement.Title, StringComparison.CurrentCultureIgnoreCase) == 0);
                    if (achievement == null)
                    {
                        achievement = new GeneratedAchievementViewModel(this, null);
                        achievements.Add(achievement);
                    }

                    achievement.Unofficial.LoadAchievement(publishedAchievement);
                    count++;
                    points += publishedAchievement.Points;
                }
            }

            PublishedAchievementCount = count;
            PublishedAchievementPoints = points;

            _logger.WriteVerbose(String.Format("Merged {0} published achievements ({1} points)", count, points));
        }

        private void MergeLocal(int gameId, List<GeneratedItemViewModelBase> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + "-User.txt");
            _localAchievements = new LocalAchievements(fileName);

            if (String.IsNullOrEmpty(_localAchievements.Title))
                _localAchievements.Title = Title;

            var localAchievements = new List<Achievement>(_localAchievements.Achievements);

            foreach (var achievement in achievements.OfType<GeneratedAchievementViewModel>())
            {
                Achievement localAchievement = null;
                if (achievement.Id > 0)
                    localAchievement = localAchievements.FirstOrDefault(a => a.Id == achievement.Id);

                if (localAchievement == null)
                {
                    localAchievement = localAchievements.FirstOrDefault(a => String.Compare(a.Title, achievement.Generated.Title.Text, StringComparison.CurrentCultureIgnoreCase) == 0);
                    if (localAchievement == null)
                    {
                        localAchievement = localAchievements.FirstOrDefault(a => a.Description == achievement.Generated.Description.Text);
                        if (localAchievement == null)
                        {
                            // TODO: attempt to match achievements by requirements                        
                            continue;
                        }
                    }
                }

                localAchievements.Remove(localAchievement);

                achievement.Local.LoadAchievement(localAchievement);
            }

            foreach (var localAchievement in localAchievements)
            {
                var vm = new GeneratedAchievementViewModel(this, null);
                vm.Local.LoadAchievement(localAchievement);
                achievements.Add(vm);
            }

            LocalAchievementCount = _localAchievements.Achievements.Count();
            LocalAchievementPoints = _localAchievements.Achievements.Sum(a => a.Points);

            _logger.WriteVerbose(String.Format("Merged {0} local achievements ({1} points)", LocalAchievementCount, LocalAchievementPoints));
        }
    }
}
