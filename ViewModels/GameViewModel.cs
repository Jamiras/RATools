using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO.Serialization;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Internal;

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
                var notes = new JsonObject(notesStream);
                foreach (var note in notes.GetField("CodeNotes").ObjectArrayValue)
                {
                    var address = Int32.Parse(note.GetField("Address").StringValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    var text = note.GetField("Note").StringValue;
                    Notes[address] = text;
                }
            }

            var achievements = new List<AchievementViewModel>(parser.Achievements.Count());
            foreach (var achievement in parser.Achievements)
            {
                var achievementViewModel = new AchievementViewModel(this, achievement);
                achievementViewModel.IsDifferentThanPublished = achievementViewModel.IsDifferentThanLocal = true;
                achievements.Add(achievementViewModel);
            }

            MergePublished(parser.GameId, achievements);
            MergeLocal(parser.GameId, achievements);

            var richPresence = new RichPresenceViewModel(this, parser.RichPresence);
            if (!String.IsNullOrEmpty(richPresence.Script) && richPresence.Script.Length > 8)
                achievements.Add(richPresence);

            foreach (var leaderboard in parser.Leaderboards)
                achievements.Add(new LeaderboardViewModel(this, leaderboard));

            Achievements = achievements;
        }

        private LocalAchievements _localAchievements;

        internal int GameId { get; private set; }
        internal string RACacheDirectory { get; private set; }
        internal TinyDictionary<int, string> Notes { get; private set; }
        
        public static readonly ModelProperty AchievementsProperty = ModelProperty.Register(typeof(GameViewModel), "Achievements", typeof(IEnumerable<AchievementViewModel>), null);
        public IEnumerable<AchievementViewModel> Achievements
        {
            get { return (IEnumerable<AchievementViewModel>)GetValue(AchievementsProperty); }
            private set { SetValue(AchievementsProperty, value); }
        }

        public static readonly ModelProperty SelectedAchievementProperty = ModelProperty.Register(typeof(GameViewModel), "SelectedAchievement", typeof(AchievementViewModel), null);
        public AchievementViewModel SelectedAchievement
        {
            get { return (AchievementViewModel)GetValue(SelectedAchievementProperty); }
            set { SetValue(SelectedAchievementProperty, value); }
        }

        internal void UpdateLocal(Achievement achievement)
        {
            var list = (List<Achievement>)_localAchievements.Achievements;
            int index = 0;
            while (index < list.Count && list[index].Title != achievement.Title)
                index++;

            if (index == list.Count)
                list.Add(achievement);
            else
                list[index] = achievement;

            _localAchievements.Commit();
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

        private void MergePublished(int gameId, List<AchievementViewModel> achievements)
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
                    var achievement = achievements.FirstOrDefault(a => a.Title == title);
                    if (achievement == null)
                    {
                        var builder = new AchievementBuilder();
                        builder.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                        builder.Title = title;
                        builder.Description = publishedAchievement.GetField("Description").StringValue;
                        builder.Points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                        builder.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;
                        builder.ParseRequirements(Tokenizer.CreateTokenizer(publishedAchievement.GetField("MemAddr").StringValue));
                        achievement = new AchievementViewModel(this, builder.ToAchievement());
                        achievement.IsNotGenerated = true;
                        achievements.Add(achievement);
                        continue;
                    }

                    achievement.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    achievement.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;

                    if (achievement.Points != publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault())
                    {
                        achievement.IsDifferentThanPublished = true;
                    }
                    else if (achievement.Description != publishedAchievement.GetField("Description").StringValue)
                    {
                        achievement.IsDifferentThanPublished = true;
                    }
                    else
                    {
                        var requirementsString = publishedAchievement.GetField("MemAddr").StringValue;
                        var cheev = new AchievementBuilder();
                        cheev.ParseRequirements(Tokenizer.CreateTokenizer(requirementsString));

                        achievement.IsDifferentThanPublished = !cheev.ToAchievement().AreRequirementsSame(achievement.Achievement);
                    }
                }

                PublishedAchievementCount = count;
                PublishedAchievementPoints = points;
            }
        }

        private void MergeLocal(int gameId, List<AchievementViewModel> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + "-User.txt");
            _localAchievements = new LocalAchievements(fileName);

            foreach (var achievement in achievements)
            {
                var localAchievement = _localAchievements.Achievements.FirstOrDefault(a => a.Title == achievement.Title);
                if (localAchievement == null)
                {
                    localAchievement = _localAchievements.Achievements.FirstOrDefault(a => a.Description == achievement.Description);
                    if (localAchievement == null)
                    {
                        // TODO: attempt to match achievements by requirements
                        continue;
                    }

                    achievement.IsDifferentThanLocal = true;
                }
                else if (achievement.Points != localAchievement.Points)
                    achievement.IsDifferentThanLocal = true;
                else if (achievement.Description != localAchievement.Description)
                    achievement.IsDifferentThanLocal = true;
                else
                    achievement.IsDifferentThanLocal = !achievement.Achievement.AreRequirementsSame(localAchievement);

                achievement.BadgeName = achievement.Achievement.BadgeName = localAchievement.BadgeName;
            }

            LocalAchievementCount = _localAchievements.Achievements.Count();
            LocalAchievementPoints = _localAchievements.Achievements.Sum(a => a.Points);
        }
    }
}
