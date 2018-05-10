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
        }

        public void SetContent(string content)
        {
            Editor.SetContent(content);
            ResetModified();
        }

        public static string GetBackupFilename(string filename)
        {
            var partial = Path.GetFileName(filename);
            return Path.Combine(Path.GetTempPath(), partial);
        }

        internal void SaveBackup(string content)
        {
            var filename = GetBackupFilename(Filename);

            // write to a tmp file so we don't destroy the actual backup if something happens while we're writing
            using (var temp = File.CreateText(filename + ".tmp"))
            {
                temp.Write(content);
            }

            // delete the previous backup (if present) and rename the tmp file
            if (File.Exists(filename))
                File.Delete(filename);
            File.Move(filename + ".tmp", filename);
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

            DeleteBackup();
        }

        public void DeleteBackup()
        { 
            var backupFilename = GetBackupFilename(Filename);
            if (File.Exists(backupFilename))
                File.Delete(backupFilename);
        }
    }
}
