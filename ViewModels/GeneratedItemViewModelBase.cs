using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using System;
using System.Diagnostics;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title}")]
    public abstract class GeneratedItemViewModelBase : ViewModelBase
    {
        public static readonly ModelProperty IdProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "Id", typeof(int), 0);
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            protected set { SetValue(IdProperty, value); }
        }

        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            protected set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty DescriptionProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "Description", typeof(string), String.Empty);
        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            protected set { SetValue(DescriptionProperty, value); }
        }

        public static readonly ModelProperty PointsProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "Points", typeof(int), 0);
        public int Points
        {
            get { return (int)GetValue(PointsProperty); }
            protected set { SetValue(PointsProperty, value); }
        }

        public virtual ModifiedState CoreModified { get { return ModifiedState.None; } }
        public virtual ModifiedState UnofficialModified { get { return ModifiedState.None; } }
        public virtual ModifiedState LocalModified { get { return ModifiedState.None; } }
        public virtual bool IsGenerated { get { return false; } }

        public CommandBase UpdateLocalCommand { get; protected set; }

        internal virtual void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e) { }
    }

    public enum ModifiedState
    {
        None = 0,
        Modified,
        Unmodified
    }
}
