using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;

namespace RATools.ViewModels
{
    internal class PlaybackEditorViewModel : EditorViewModel
    {
        public PlaybackEditorViewModel(GameViewModel owner, string filename)
            : base(owner)
        {
            _owner = owner;
            _filename = filename;
        }

        private System.IO.TextReader _reader;
        private readonly GameViewModel _owner;
        private readonly string _filename;

        protected override void OnUpdateSyntax(ContentChangedEventArgs e)
        {
            if (e.Type == ContentChangeType.Refresh && _reader == null)
            {
                _owner.Script.Filename = "recordings\\" + _filename + ".rascript";

                _reader = System.IO.File.OpenText("recordings\\" + _filename + ".txt");
                ServiceRepository.Instance.FindService<IBackgroundWorkerService>().RunAsync(DoPlayback);
                return;
            }

            base.OnUpdateSyntax(e);
        }

        private void DoPlayback()
        {
            var backgroundWorkerService = ServiceRepository.Instance.FindService<IBackgroundWorkerService>();
            string line;
            {
                var builder = new StringBuilder();

                while ((line = _reader.ReadLine()) != null)
                {
                    if (line == "==-==-==-==-==-==")
                        break;

                    builder.AppendLine(line);
                }

                backgroundWorkerService.InvokeOnUiThread(() =>
                {
                    SetContent(builder.ToString());
                });
            }

            int totalTime = 0;
            var stopwatch = Stopwatch.StartNew();

            while ((line = _reader.ReadLine()) != null)
            {
                var parts = line.Split(' ');
                var delay = Int32.Parse(parts[0]);
                totalTime += delay;
                if (delay > 1000)
                    delay = 1000;
                if (delay > 5) // adjust the sleep just a little bit to accomodate for thread marshalling
                    delay -= 5;

                System.Threading.Thread.Sleep(delay);

                if (parts.Length == 2)
                {
                    if (parts[1].Length == 1)
                    {
                        TypeCharacter(parts[1][0]);
                    }
                    else
                    {
                        var modifiers = ModifierKeys.None;
                        foreach (var modifier in parts[1].Split('+'))
                        {
                            if (modifier == "Ctrl")
                                modifiers |= ModifierKeys.Control;
                            else if (modifier == "Shift")
                                modifiers |= ModifierKeys.Shift;
                            else if (modifier == "Alt")
                                modifiers |= ModifierKeys.Alt;
                            else
                            {
                                Key key;
                                if (Enum.TryParse(modifier, out key))
                                {
                                    if (key == Key.S && modifiers == ModifierKeys.Control)
                                    {
                                        _owner.Script.ResetModified();
                                    }
                                    else
                                    {
                                        var e = new KeyPressedEventArgs(key, modifiers);
                                        backgroundWorkerService.InvokeOnUiThread(() =>
                                        {
                                            OnKeyPressed(e);
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    var flags = MoveCursorFlags.None;
                    if (parts[1] == "Shift+Click")
                        flags = MoveCursorFlags.Highlighting;

                    var location = parts[2].Split(',');
                    var clickLine = Int32.Parse(location[0]);
                    var clickColumn = Int32.Parse(location[1]);

                    backgroundWorkerService.InvokeOnUiThread(() =>
                    {
                        MoveCursorTo(clickLine, clickColumn, flags);
                    });
                }
            }

            _reader.Close();

            stopwatch.Stop();

            var totalElapsed = TimeSpan.FromMilliseconds(totalTime);
            var playbackElapsed = stopwatch.Elapsed;

            backgroundWorkerService.InvokeOnUiThread(() =>
            {
                MessageBoxViewModel.ShowMessage(String.Format("Playback complete.\n\n" +
                    "Original time: {0}\nPlayback time: {1}", totalElapsed, playbackElapsed));
            });
        }
    }
}
