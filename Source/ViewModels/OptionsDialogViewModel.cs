using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;

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

            ApiKey = new SecretTextFieldViewModel("ApiKey", 32);
            ApiKey.SecretText = settings.ApiKey;

            SettingsLinkCommand = new DelegateCommand(OpenSettingsLink);

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

            var colors = new List<ColorViewModel>();
            colors.Add(new ColorViewModel(Theme.Color.Background, "Background"));
            colors.Add(new ColorViewModel(Theme.Color.Foreground, "Foreground"));
            colors.Add(new ColorViewModel(Theme.Color.ScrollBarBackground, "ScrollBar Background"));
            colors.Add(new ColorViewModel(Theme.Color.ScrollBarForeground, "ScrollBar Foreground"));
            colors.Add(new ColorViewModel(Theme.Color.EditorSelection, "Editor Selection"));
            colors.Add(new ColorViewModel(Theme.Color.EditorLineNumbers, "Editor Line Numbers"));
            colors.Add(new ColorViewModel(Theme.Color.EditorKeyword, "Editor Keyword"));
            colors.Add(new ColorViewModel(Theme.Color.EditorComment, "Editor Comment"));
            colors.Add(new ColorViewModel(Theme.Color.EditorIntegerConstant, "Editor Integer Constant"));
            colors.Add(new ColorViewModel(Theme.Color.EditorStringConstant, "Editor String Constant"));
            colors.Add(new ColorViewModel(Theme.Color.EditorVariable, "Editor Variable"));
            colors.Add(new ColorViewModel(Theme.Color.EditorFunctionDefinition, "Editor Function Definition"));
            colors.Add(new ColorViewModel(Theme.Color.EditorFunctionCall, "Editor Function Call"));
            colors.Add(new ColorViewModel(Theme.Color.DiffAdded, "Change Added"));
            colors.Add(new ColorViewModel(Theme.Color.DiffRemoved, "Change Removed"));
            Colors = colors;

            DefaultColorsCommand = new DelegateCommand(DefaultColors);
            DarkColorsCommand = new DelegateCommand(DarkColors);
            ExportColorsCommand = new DelegateCommand(ExportColors);
            ImportColorsCommand = new DelegateCommand(ImportColors);
        }

        private readonly ISettings _settings;
        private readonly IFileSystemService _fileSystemService;

        public TextFieldViewModel UserName { get; private set; }

        public SecretTextFieldViewModel ApiKey { get; private set; }

        public CommandBase SettingsLinkCommand { get; private set; }

        private void OpenSettingsLink()
        {
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl("https://retroachievements.org/controlpanel.php");
        }

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
            settings.ApiKey = ApiKey.SecretText;

            settings.EmulatorDirectories.Clear();
            foreach (var directoryViewModel in Directories)
                settings.EmulatorDirectories.Add(directoryViewModel.Path);

            settings.Colors = Theme.Serialize();

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

        public class ColorViewModel : ViewModelBase
        {
            public ColorViewModel(Theme.Color color, string label)
            {
                Label = label;
                _themeColor = color;
                _color = _originalColor = Theme.GetColor(color);

                ChangeColorCommand = new DelegateCommand(ChangeColor);
            }

            public void Reset()
            {
                if (_color != _originalColor)
                    Theme.SetColor(_themeColor, _originalColor);
            }

            public void UpdateColor()
            {
                Color = Theme.GetColor(_themeColor);
            }

            /// <summary>
            /// Gets or sets the item Label.
            /// </summary>
            public string Label { get; private set; }

            /// <summary>
            /// Gets or sets the current Color.
            /// </summary>
            public Color Color
            {
                get { return _color; }
                set
                {
                    if (_color != value)
                    {
                        _color = value;
                        OnPropertyChanged(() => Color);

                        Theme.SetColor(_themeColor, value);
                    }
                }
            }
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private Color _color;

            private readonly Theme.Color _themeColor;
            private Color _originalColor;

            public CommandBase ChangeColorCommand { get; private set; }
            private void ChangeColor()
            {
                var vmColorPicker = new ColorPickerDialogViewModel();
                vmColorPicker.SelectedColor = Color;

                if (vmColorPicker.ShowDialog() == DialogResult.Ok)
                    Color = vmColorPicker.SelectedColor;
            }
        }

        public IEnumerable<ColorViewModel> Colors { get; private set; }


        public CommandBase ExportColorsCommand { get; private set; }
        private void ExportColors()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export Colors";
            vm.Filters["JSON file"] = "*.json";
            vm.FileNames = new[] { "Colors.json" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                Theme.Export(vm.FileNames[0]);
            }
        }

        public CommandBase ImportColorsCommand { get; private set; }
        private void ImportColors()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Import Colors";
            vm.Filters["JSON file"] = "*.json";
            vm.FileNames = new[] { "Colors.json" };
            vm.CheckFileExists = true;

            if (vm.ShowOpenFileDialog() == DialogResult.Ok)
            {
                Theme.Import(vm.FileNames[0]);
            }
        }

        public CommandBase DefaultColorsCommand { get; private set; }
        private void DefaultColors()
        {
            Theme.InitDefault();

            foreach (var color in Colors)
                color.UpdateColor();
        }

        public CommandBase DarkColorsCommand { get; private set; }
        private void DarkColors()
        {
            Theme.InitDark();

            foreach (var color in Colors)
                color.UpdateColor();
        }

        protected override void ExecuteCancelCommand()
        {
            foreach (var color in Colors)
                color.Reset();

            base.ExecuteCancelCommand();
        }
    }
}
