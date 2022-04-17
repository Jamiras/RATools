using Jamiras.ViewModels;
using System;

namespace RATools.ViewModels
{
    internal class RecordingEditorViewModel : EditorViewModel
    {
        public RecordingEditorViewModel(GameViewModel owner)
            : base(owner)
        {
        }

        private System.IO.TextWriter _recorder;
        private System.Diagnostics.Stopwatch _recordingStopwatch;
        private bool _inKeyPressed = false;

        public override void Dispose()
        {
            EndRecording();
            base.Dispose();
        }

        public void EndRecording()
        {
            if (_recorder != null)
            {
                _recorder.Flush();
                _recorder.Close();
                _recorder = null;
            }
        }

        protected override void OnUpdateSyntax(ContentChangedEventArgs e)
        {
            if (e.Type == ContentChangeType.Refresh)
            {
                EndRecording();

                var now = DateTime.Now;
                _recorder = System.IO.File.CreateText(
                    String.Format("recordings\\{0:D4}{1:D2}{2:D2}-{3:D2}{4:D2}{5:D2}.txt",
                                  now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second));
                _recorder.Write(e.Content);
                _recorder.WriteLine("==-==-==-==-==-==");
                _recorder.Flush();
                _recordingStopwatch = System.Diagnostics.Stopwatch.StartNew();
            }

            base.OnUpdateSyntax(e);
        }

        protected override void OnKeyPressed(KeyPressedEventArgs e)
        {
            _inKeyPressed = true;

            switch (e.Key)
            {
                case System.Windows.Input.Key.LeftCtrl:
                case System.Windows.Input.Key.LeftAlt:
                case System.Windows.Input.Key.LeftShift:
                case System.Windows.Input.Key.RightCtrl:
                case System.Windows.Input.Key.RightAlt:
                case System.Windows.Input.Key.RightShift:
                    // don't record these
                    break;

                default:
                    _recordingStopwatch.Stop();
                    var c = ((e.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0) ? e.GetChar() : '\0';
                    if (c != '\0' && c != ' ')
                    {
                        _recorder.WriteLine(String.Format("{0} {1}", _recordingStopwatch.ElapsedMilliseconds, c));
                    }
                    else
                    {
                        _recorder.WriteLine(String.Format("{0} {1}{2}{3}{4}", _recordingStopwatch.ElapsedMilliseconds,
                            ((e.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) ? "Ctrl+" : "",
                            ((e.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0) ? "Alt+" : "",
                            ((e.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) ? "Shift+" : "",
                            e.Key));
                    }
                    _recordingStopwatch.Restart();
                    break;
            }

            base.OnKeyPressed(e);

            _inKeyPressed = false;
        }

        public override void MoveCursorTo(int line, int column, MoveCursorFlags flags)
        {
            if (!_inKeyPressed)
            {
                _recordingStopwatch.Stop();
                _recorder.WriteLine(String.Format("{0} {1}Click {2},{3}", _recordingStopwatch.ElapsedMilliseconds,
                    ((flags & MoveCursorFlags.Highlighting) != 0) ? "Shift+" : "",
                    line, column));
                _recordingStopwatch.Restart();
            }

            base.MoveCursorTo(line, column, flags);
        }
    }
}
