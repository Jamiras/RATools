using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace RATools.ViewModels
{
    public class OptionsDialogViewModel : DialogViewModelBase
    {
        public OptionsDialogViewModel()
            : this(ServiceRepository.Instance.FindService<ISettings>(),
                   ServiceRepository.Instance.FindService<IFileSystemService>())
        {
        }

        public OptionsDialogViewModel(ISettings settings, IFileSystemService fileSystemService)
        {
            _settings = settings;
            _fileSystemService = fileSystemService;

            UserName = new TextFieldViewModel("UserName", 80);
            UserName.Text = settings.UserName;

            Directories = new ObservableCollection<DirectoryViewModel>();
            foreach (var path in settings.EmulatorDirectories)
            {
                var directoryViewModel = new DirectoryViewModel { Path = path };

                var cachePath = Path.Combine(path, "RACache", "Data");
                directoryViewModel.IsValid = fileSystemService.DirectoryExists(cachePath);

                Directories.Add(directoryViewModel);
            }

            DialogTitle = "Settings";

            AddDirectoryCommand = new DelegateCommand(AddDirectory);
            RemoveDirectoryCommand = new DelegateCommand(RemoveDirectory);
        }

        private readonly ISettings _settings;
        private readonly IFileSystemService _fileSystemService;

        public TextFieldViewModel UserName { get; private set; }

        public class DirectoryViewModel
        {
            public string Path { get; set; }
            public bool IsValid { get; set; }
        }

        public ObservableCollection<DirectoryViewModel> Directories { get; private set; }

        public static readonly ModelProperty SelectedDirectoryProperty = ModelProperty.Register(typeof(OptionsDialogViewModel), "SelectedDirectory", typeof(DirectoryViewModel), null);

        public DirectoryViewModel SelectedDirectory
        {
            get { return (DirectoryViewModel)GetValue(SelectedDirectoryProperty); }
            set { SetValue(SelectedDirectoryProperty, value); }
        }

        public void ApplyChanges()
        {
            var settings = (Settings)_settings;
            settings.UserName = UserName.Text;

            settings.EmulatorDirectories.Clear();
            foreach (var directoryViewModel in Directories)
                settings.EmulatorDirectories.Add(directoryViewModel.Path);

            settings.Save();
        }

        public CommandBase AddDirectoryCommand { get; private set; }
        private void AddDirectory()
        {
            var vm = new FileDialogViewModel();

            if (vm.ShowSelectFolderDialog() == DialogResult.Ok)
            {
                var directoryViewModel = new DirectoryViewModel { Path = vm.FileNames[0] };

                var cachePath = Path.Combine(vm.FileNames[0], "RACache", "Data");
                directoryViewModel.IsValid = _fileSystemService.DirectoryExists(cachePath);

                Directories.Add(directoryViewModel);
            }
        }

        public CommandBase RemoveDirectoryCommand { get; private set; }
        private void RemoveDirectory()
        {
            var selectedDirectory = SelectedDirectory;
            if (selectedDirectory != null)
                Directories.Remove(selectedDirectory);
        }
    }
}
