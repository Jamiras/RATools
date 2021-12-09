﻿using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using System;
using System.Diagnostics;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title}")]
    public abstract class ViewerViewModelBase : ViewModelBase
    {
        public ViewerViewModelBase(GameViewModel owner)
        {
            _owner = owner;
        }

        protected readonly GameViewModel _owner;

        public abstract string ViewerType { get; }

        public virtual string ViewerImage
        {
            get { return String.Format("/RATools;component/Resources/{0}.png", ViewerType.ToLower()); }
        }

        public CommandBase UpdateLocalCommand { get; protected set; }

        internal string RACacheDirectory
        {
            get { return _owner.RACacheDirectory; }
        }

        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            protected set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty DescriptionProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "Description", typeof(string), String.Empty);
        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            protected set { SetValue(DescriptionProperty, value); }
        }

        public static readonly ModelProperty IsTitleModifiedProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "IsTitleModified", typeof(bool), false);
        public bool IsTitleModified
        {
            get { return (bool)GetValue(IsTitleModifiedProperty); }
            protected set { SetValue(IsTitleModifiedProperty, value); }
        }

        public static readonly ModelProperty IsDescriptionModifiedProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "IsDescriptionModified", typeof(bool), false);
        public bool IsDescriptionModified
        {
            get { return (bool)GetValue(IsDescriptionModifiedProperty); }
            protected set { SetValue(IsDescriptionModifiedProperty, value); }
        }

        public static readonly ModelProperty ModificationMessageProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "ModificationMessage", typeof(string), null);
        public string ModificationMessage
        {
            get { return (string)GetValue(ModificationMessageProperty); }
            protected set { SetValue(ModificationMessageProperty, value); }
        }

        public static readonly ModelProperty CompareStateProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "CompareState", typeof(GeneratedCompareState), GeneratedCompareState.None);
        public GeneratedCompareState CompareState
        {
            get { return (GeneratedCompareState)GetValue(CompareStateProperty); }
            protected set { SetValue(CompareStateProperty, value); }
        }

        public static readonly ModelProperty CanUpdateProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "CanUpdate", typeof(bool), false);
        public bool CanUpdate
        {
            get { return (bool)GetValue(CanUpdateProperty); }
            protected set { SetValue(CanUpdateProperty, value); }
        }
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
        PublishedMatchesNotLocal,
    }
}
