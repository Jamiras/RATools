using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using RATools.Parser;
using System;
using System.IO;

namespace RATools.ViewModels
{
    public class ScriptViewModel : GeneratedItemViewModelBase
    {
        public ScriptViewModel(GameViewModel owner)
        {
            _owner = owner;
            Title = "Script";
            Editor = new EditorViewModel();
            Editor.LineChanged += (o, e) =>
            {
                ModificationMessage = "Script differs from disk";
                CompareState = GeneratedCompareState.LocalDiffers;
            };
        }

        private GameViewModel _owner;

        public override bool IsGenerated { get { return true; } }

        public EditorViewModel Editor { get; private set; }

        public static readonly ModelProperty FilenameProperty = ModelProperty.Register(typeof(ScriptViewModel), "Filename", typeof(string), null, OnFilenameChanged);
        public string Filename
        {
            get { return (string)GetValue(FilenameProperty); }
            set { SetValue(FilenameProperty, value); }
        }

        private static void OnFilenameChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var vm = (ScriptViewModel)sender;
            var filename = (string)e.NewValue;
            vm.Title = String.IsNullOrEmpty(filename) ? "Script" : Path.GetFileName(filename);

            vm._owner.PopulateEditorList(null);
        }

        public static readonly ModelProperty ContentProperty = ModelProperty.Register(typeof(ScriptViewModel), "Content", typeof(string), String.Empty, OnContentChanged);
        public string Content
        {
            get { return (string)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        private static void OnContentChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var vm = (ScriptViewModel)sender;
            ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(() =>
            {
                vm.Editor.SetContent((string)e.NewValue);

                var interpreter = new AchievementScriptInterpreter();
                if (interpreter.Run(vm.Editor.ParsedContent))
                    vm._owner.PopulateEditorList(interpreter);
                else
                    vm._owner.PopulateEditorList(null);
            });
        }
    }
}
