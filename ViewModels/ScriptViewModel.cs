using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
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
            Editor = new EditorViewModel(owner);
            Editor.LineChanged += (o, e) => SetModified();
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

            //vm._owner.PopulateEditorList(null);
        }

        public void SetContent(string content)
        {
            Editor.SetContent(content);
            ResetModified();
        }

        public void Save()
        {
            Save(Filename);
            ResetModified();
        }

        public void SetModified()
        {
            ModificationMessage = "Script differs from disk";
            CompareState = GeneratedCompareState.LocalDiffers;
        }

        private void ResetModified()
        { 
            CompareState = GeneratedCompareState.Same;
            ModificationMessage = null;
        }

        private void Save(string filename)
        {
            using (var file = new StreamWriter(ServiceRepository.Instance.FindService<IFileSystemService>().CreateFile(filename)))
            {
                foreach (var line in Editor.Lines)
                    file.WriteLine(line.Text);
            }
        }
    }
}
