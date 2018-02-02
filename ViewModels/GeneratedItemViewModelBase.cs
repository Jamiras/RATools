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

        public virtual bool IsGenerated { get { return false; } }

        public static readonly ModelProperty ModificationMessageProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "ModificationMessage", typeof(string), null);
        public string ModificationMessage
        {
            get { return (string)GetValue(ModificationMessageProperty); }
            protected set { SetValue(ModificationMessageProperty, value); }
        }

        public static readonly ModelProperty CompareStateProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "CompareState", typeof(GeneratedCompareState), GeneratedCompareState.None);
        public GeneratedCompareState CompareState
        {
            get { return (GeneratedCompareState)GetValue(CompareStateProperty); }
            protected set { SetValue(CompareStateProperty, value); }
        }

        public static readonly ModelProperty CanUpdateProperty = ModelProperty.Register(typeof(GeneratedItemViewModelBase), "CanUpdate", typeof(bool), false);
        public bool CanUpdate
        {
            get { return (bool)GetValue(CanUpdateProperty); }
            protected set { SetValue(CanUpdateProperty, value); }
        }

        public CommandBase UpdateLocalCommand { get; protected set; }

        internal virtual void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e) { }
    }

    public enum ModifiedState
    {
        None = 0,
        Modified,
        Unmodified
    }

    public enum GeneratedCompareState
    {
        /// <summary>
        /// Not generated
        /// </summary>
        None = 0,

        /// <summary>
        /// Same as published and/or local
        /// </summary>
        Same,

        /// <summary>
        /// Differs from local value
        /// </summary>
        LocalDiffers,

        /// <summary>
        /// Differs from published value
        /// </summary>
        PublishedDiffers,

        /// <summary>
        /// Same as published but not in local
        /// </summary>
        PublishedMatchesNotGenerated,
    }
}
