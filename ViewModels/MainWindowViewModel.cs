using System.IO;
using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.IO;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Parser;

namespace RATools.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            ExitCommand = new DelegateCommand(Exit);
            CompileAchievementsCommand = new DelegateCommand(CompileAchievements);
        }

        public bool Initialize()
        {
            var file = new IniFile("RATools.ini");
            try
            {
                var values = file.Read();
                RACacheDirectory = values["RACacheDirectory"];
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private string RACacheDirectory;

        public CommandBase ExitCommand { get; private set; }

        private void Exit()
        {
            ServiceRepository.Instance.FindService<IDialogService>().MainWindow.Close();
        }

        public CommandBase CompileAchievementsCommand { get; private set; }

        private void CompileAchievements()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Select achievements script";
            vm.Filters["Script file"] = "*.txt";
            vm.CheckFileExists = true;

            if (vm.ShowOpenFileDialog() == DialogResult.Ok)
            {
                using (var stream = File.OpenRead(vm.FileNames[0]))
                {
                    var parser = new AchievementScriptParser();
                    if (parser.Parse(Tokenizer.CreateTokenizer(stream)))
                    {
                    }
                }
            }
        }
    }
}
