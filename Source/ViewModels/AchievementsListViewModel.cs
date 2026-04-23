using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.ViewModels.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RATools.ViewModels
{
    public class AchievementsListViewModel : ViewerViewModelBase
    {
        public AchievementsListViewModel(GameViewModel owner, AchievementSet achievementSet, IEnumerable<NavigationViewModelBase> navigationNodes)
            : base(owner)
        {
            AchievementViewModel[] achievements;

            if (navigationNodes != null)
            {
                achievements = navigationNodes.OfType<AchievementNavigationViewModel>()
                    .Select(vm => vm.Editor as AchievementViewModel).ToArray();
            }
            else
            {
                achievements = owner.Editors.OfType<AchievementViewModel>()
                    .Where(a => a.BelongsToSet(achievementSet)).ToArray();
            }

            Achievements = achievements;

            Title = achievementSet.Title;
            CountAchievements(achievements);
            _badgeName = achievementSet?.BadgeName ?? owner.BadgeName;

            _id = (achievementSet != null) ? achievementSet.Id : 0;

            ExportCommand = new DelegateCommand(Export);
        }

        private readonly int _id;

        private void CountAchievements(AchievementViewModel[] achievements)
        {
            var description = new StringBuilder();
            description.AppendFormat("{0} achievements", achievements.Length);
            var initialDescriptionLength = description.Length;
            description.Append(" (");

            var points = new StringBuilder();
            points.AppendFormat("{0} points", achievements.Sum(a => a.Points));
            var initialPointsLength = points.Length;
            points.Append(" (");

            var published = achievements.Where(a => !a.Published?.Asset?.IsUnofficial ?? false);
            if (published.Any())
            {
                description.AppendFormat("{0} published, ", published.Count());
                points.AppendFormat("{0} published, ", published.Sum(a => a.Points));
            }

            var unpublished = achievements.Where(a => a.Published?.Asset?.IsUnofficial ?? false);
            if (unpublished.Any())
            {
                description.AppendFormat("{0} unpublished, ", unpublished.Count());
                points.AppendFormat("{0} unpublished, ", unpublished.Sum(a => a.Points));
            }

            var local = achievements.Where(a => a.Published?.Asset == null);
            if (local.Any())
            {
                description.AppendFormat("{0} local, ", local.Count());
                points.AppendFormat("{0} local, ", local.Sum(a => a.Points));
            }

            description.Length -= 2;
            description.Append(')');

            points.Length -= 2;
            points.Append(')');

            bool hasComma = false;
            for (int i = initialDescriptionLength; i < description.Length; i++)
            {
                if (description[i] == ',')
                {
                    hasComma = true;
                    break;
                }
            }
            if (!hasComma)
            {
                description.Length = initialDescriptionLength;
                points.Length = initialPointsLength;
            }

            Description = description.ToString();
            PointsSummary = points.ToString();
        }

        private readonly string _badgeName;

        public override string ViewerType => "AchievementList";

        public override int ViewerId { get { return _id; } }

        public static readonly ModelProperty BadgeProperty = ModelProperty.RegisterDependant(typeof(AchievementsListViewModel), "Badge", typeof(ImageSource), new ModelProperty[0], GetBadge);
        public ImageSource Badge
        {
            get { return (ImageSource)GetValue(BadgeProperty); }
        }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (AchievementsListViewModel)model;
            if (!String.IsNullOrEmpty(vm._badgeName) && vm._badgeName != "0")
            {
                if (String.IsNullOrEmpty(vm._owner.RACacheDirectory))
                    return null;

                var name = "i" + vm._badgeName;
                if (Path.GetExtension(name) == "")
                    name += ".png";
                var path = Path.Combine(Path.Combine(vm._owner.RACacheDirectory, "..\\Badge"), name);
                if (File.Exists(path))
                {
                    var image = new BitmapImage(new Uri(path));
                    image.Freeze();
                    return image;
                }
            }

            return null;
        }

        public static readonly ModelProperty PointsSummaryProperty = ModelProperty.Register(typeof(AchievementsListViewModel), "PointsSummary", typeof(string), String.Empty);
        public string PointsSummary
        {
            get { return (string)GetValue(PointsSummaryProperty); }
            protected set { SetValue(PointsSummaryProperty, value); }
        }

        /// <summary>
        /// Gets the list of achievements.
        /// </summary>
        public IEnumerable<AchievementViewModel> Achievements { get; private set; }

        public int Points {  get { return Achievements.Sum(a => a.Points); } }

        public CommandBase ExportCommand { get; private set; }

        private void Export()
        {
            var filename = Path.GetFileNameWithoutExtension(_owner.Script.Filename) ?? "achievements";

            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export achievement information";
            vm.Filters["CSV file"] = "*.csv";
            vm.FileNames = new[] { filename + ".csv" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                using (var file = File.CreateText(vm.FileNames[0]))
                {
                    file.WriteLine("Id,Title,Description,Points");

                    foreach (var achievement in Achievements)
                    {
                        file.Write("{0},\"{1}\",", achievement.Id, achievement.Title.Replace("\"", "\\\""));
                        file.Write("\"{0}\",", achievement.Description.Replace("\"", "\\\""));
                        file.Write("{0}", achievement.Points);
                        file.WriteLine();
                    }
                }
            }
        }
    }
}
