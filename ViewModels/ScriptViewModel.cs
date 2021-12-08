//#define DEBUG_RECORDING
//#define DEBUG_PLAYBACK

using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RATools.ViewModels
{
    public class ScriptViewModel : GeneratedItemViewModelBase
    {
        public ScriptViewModel(GameViewModel owner)
            : this()
        {
#if DEBUG_PLAYBACK
            Editor = new PlaybackEditorViewModel(owner, "20210728-185730");
#elif DEBUG_RECORDING
            Editor = new RecordingEditorViewModel(owner);
#else
            Editor = new EditorViewModel(owner);
#endif

            Editor.LineChanged += (o, e) => SetModified();
        }

        protected ScriptViewModel()
            : base(null)
        {
            Title = "Script";
            UpdateLocalCommand = DisabledCommand.Instance;
        }

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

        internal void ResetModified()
        { 
            CompareState = GeneratedCompareState.Same;
            ModificationMessage = null;
        }

        private void Save(string filename)
        {
            using (var file = new StreamWriter(ServiceRepository.Instance.FindService<IFileSystemService>().CreateFile(filename)))
            {
                var enumerator = Editor.Lines.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    file.Write(enumerator.Current.Text);
                    while (enumerator.MoveNext())
                    {
                        file.WriteLine();
                        file.Write(enumerator.Current.Text);
                    }
                }
            }

            DeleteBackup();
        }

        public void DeleteBackup()
        { 
            var backupFilename = GetBackupFilename(Filename);
            if (File.Exists(backupFilename))
                File.Delete(backupFilename);
        }

        internal void OnBeforeClose()
        {
#if DEBUG_RECORDING
            ((RecordingEditorViewModel)Editor).EndRecording();
#endif
        }
    }
}
