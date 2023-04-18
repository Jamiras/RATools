using Jamiras.Commands;
using Jamiras.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

namespace RATools.ViewModels
{
    public class ViewAchievementsViewModel : DialogViewModelBase
    {
        public ViewAchievementsViewModel(GameViewModel game)
        {
            _game = game;

            DialogTitle = "View Achievements - " + game.Title;
            CanClose = true;
            CancelButtonText = null;
            ExtraButtonCommand = new DelegateCommand(Export);

            Achievements = game.Editors.OfType<AchievementViewModel>().ToArray();
        }

        private readonly GameViewModel _game;

        /// <summary>
        /// Gets the list of achievements.
        /// </summary>
        public IEnumerable<AchievementViewModel> Achievements { get; private set; }

        public string ExtraButtonText {  get { return "Export"; } }
        public CommandBase ExtraButtonCommand { get; private set; }

        private void Export()
        {
            var filename = Path.GetFileNameWithoutExtension(_game.Script.Filename) ?? "achievements";

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
