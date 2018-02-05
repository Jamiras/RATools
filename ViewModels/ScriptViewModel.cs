using Jamiras.DataModels;
using RATools.Parser;
using System;

namespace RATools.ViewModels
{
    public class ScriptViewModel : GeneratedItemViewModelBase
    {
        public ScriptViewModel(GameViewModel owner)
        {
            _owner = owner;

            Title = "Script";
            Editor = new EditorViewModel();
        }

        private GameViewModel _owner;

        public override bool IsGenerated { get { return true; } }

        public EditorViewModel Editor { get; private set; }

        public string Filename { get; private set; }

        public static readonly ModelProperty ContentProperty = ModelProperty.Register(typeof(ScriptViewModel), "Content", typeof(string), String.Empty, OnContentChanged);
        public string Content
        {
            get { return (string)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        private static void OnContentChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var vm = (ScriptViewModel)sender;
            vm.Editor.Content = (string)e.NewValue;

            var interpreter = new AchievementScriptInterpreter();
            if (interpreter.Run(vm.Editor.ParsedContent))
                vm._owner.PopulateEditorList(interpreter);
            else
                vm._owner.PopulateEditorList(new AchievementScriptInterpreter());
        }
    }
}
