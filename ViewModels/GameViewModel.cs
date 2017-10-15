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

        internal void UpdateLocal(Achievement achievement, string localTitle)
        {
            var list = (List<Achievement>)_localAchievements.Achievements;
            int index = 0;
            while (index < list.Count && list[index].Title != localTitle)
                index++;

            if (index == list.Count)
            {
                list.Add(achievement);
                LocalAchievementCount++;
                LocalAchievementPoints += achievement.Points;
            }
            else
            {
                var diff = achievement.Points - list[index].Points;
                if (diff != 0)
                    LocalAchievementPoints += diff;
                list[index] = achievement;
            }

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

        private static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
                    var achievement = achievements.FirstOrDefault(a => a.Title.Text == title);
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
                        achievement.Title.PublishedText = achievement.Title.Text;
                        achievement.Description.PublishedText = achievement.Description.Text;
                        achievement.Points.PublishedText = achievement.Points.Text;
                        achievement.Title.IsNotGenerated = true;
                        achievement.Achievement.Published = UnixEpoch.AddSeconds(publishedAchievement.GetField("Created").IntegerValue.GetValueOrDefault());
                        achievement.Achievement.LastModified = UnixEpoch.AddSeconds(publishedAchievement.GetField("Modified").IntegerValue.GetValueOrDefault());
                        
                        foreach (var requirementGroup in achievement.RequirementGroups)
                        {
                            foreach (var requirement in requirementGroup.Requirements)
                                requirement.Definition.PublishedText = requirement.Definition.Text;
                        }
                        
                        achievements.Add(achievement);
                        continue;
                    }

                    achievement.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    achievement.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;

                    achievement.Title.PublishedText = title;
                    achievement.Points.PublishedText = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault().ToString();
                    achievement.Description.PublishedText = publishedAchievement.GetField("Description").StringValue;

                    var requirementsString = publishedAchievement.GetField("MemAddr").StringValue;
                    var cheev = new AchievementBuilder();
                    cheev.ParseRequirements(Tokenizer.CreateTokenizer(requirementsString));
                    MergeRequirements(achievement, cheev.ToAchievement(), false);
                }

                PublishedAchievementCount = count;
                PublishedAchievementPoints = points;
            }
        }

        private void MergeLocal(int gameId, List<AchievementViewModel> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + "-User.txt");
            _localAchievements = new LocalAchievements(fileName);

            if (String.IsNullOrEmpty(_localAchievements.Title))
                _localAchievements.Title = Title;

            var localAchievements = new List<Achievement>(_localAchievements.Achievements);

            foreach (var achievement in achievements)
            {
                var localAchievement = localAchievements.FirstOrDefault(a => a.Title == achievement.Title.Text);
                if (localAchievement == null)
                {
                    localAchievement = localAchievements.FirstOrDefault(a => a.Description == achievement.Description.Text);
                    if (localAchievement == null)
                    {
                        // TODO: attempt to match achievements by requirements
                        achievement.Title.IsNewLocal = true;
                        continue;
                    }
                }

                localAchievements.Remove(localAchievement);

                achievement.Title.LocalText = localAchievement.Title;
                achievement.Points.LocalText = localAchievement.Points.ToString();
                achievement.Description.LocalText = localAchievement.Description;

                MergeRequirements(achievement, localAchievement, true);

                achievement.BadgeName = achievement.Achievement.BadgeName = localAchievement.BadgeName;
            }

            foreach (var localAchievement in localAchievements)
            {
                var vm = new AchievementViewModel(this, localAchievement);
                vm.Title.IsNotGenerated = true;
                achievements.Add(vm);
            }

            LocalAchievementCount = _localAchievements.Achievements.Count();
            LocalAchievementPoints = _localAchievements.Achievements.Sum(a => a.Points);
        }

        private void MergeRequirements(AchievementViewModel achievementViewModel, Achievement achievement, bool isLocal)
        {
            var enumerable = achievementViewModel.RequirementGroups.GetEnumerator();
            if (!enumerable.MoveNext())
                return;

            MergeRequirements(enumerable.Current, achievement.CoreRequirements, isLocal);

            foreach (var alt in achievement.AlternateRequirements)
            {
                if (enumerable.MoveNext())
                    MergeRequirements(enumerable.Current, alt, isLocal);
            }

            while (enumerable.MoveNext())
            {
                foreach (var requirement in enumerable.Current.Requirements)
                {
                    if (isLocal)
                        requirement.Definition.IsNewLocal = true;
                    else
                        requirement.Definition.PublishedText = "New";
                }
            }
        }

        private void MergeRequirements(RequirementGroupViewModel requirementGroupViewModel, IEnumerable<Requirement> requirements, bool isLocal)
        {
            var enumerable = requirementGroupViewModel.Requirements.GetEnumerator();
            var unmatchedRequirements = new List<Requirement>(requirements);

            while (enumerable.MoveNext())
            {
                Requirement requirement = null;
                for (int i = 0; i < unmatchedRequirements.Count; i++)
                {
                    if (unmatchedRequirements[i] == enumerable.Current.Requirement)
                    {
                        requirement = unmatchedRequirements[i];
                        unmatchedRequirements.RemoveAt(i);
                        break;
                    }
                }

                if (requirement == null && enumerable.Current.Requirement.Left.Type == FieldType.MemoryAddress)
                {
                    for (int i = 0; i < unmatchedRequirements.Count; i++)
                    {
                        if (unmatchedRequirements[i].Left.Type == FieldType.MemoryAddress && 
                            unmatchedRequirements[i].Left.Value == enumerable.Current.Requirement.Left.Value)
                        {
                            requirement = unmatchedRequirements[i];
                            unmatchedRequirements.RemoveAt(i);
                            break;
                        }
                    }
                }

                if (requirement != null)
                {
                    if (isLocal)
                        enumerable.Current.Definition.LocalText = requirement.ToString();
                    else
                        enumerable.Current.Definition.PublishedText = requirement.ToString();
                }
                else
                {
                    if (isLocal)
                        enumerable.Current.Definition.IsNewLocal = true;
                    else
                        enumerable.Current.Definition.PublishedText = "New";
                }
            }

            foreach (var unmatchedRequirement in unmatchedRequirements)
            {
                var unmatchedRequirementViewModel = new RequirementViewModel(unmatchedRequirement, Notes);
                unmatchedRequirementViewModel.Definition.IsNotGenerated = true;
                ((ICollection<RequirementViewModel>)requirementGroupViewModel.Requirements).Add(unmatchedRequirementViewModel);
            }
        }
    }
}
