﻿using Jamiras.Components;
using System;
using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Provides common properties for an asset.
    /// </summary>
    [DebuggerDisplay("{Title}")]
    public class AssetBase
    {
        /// <summary>
        /// Gets the unique identifier of the asset.
        /// </summary>
        public int Id { get; internal set; }

        /// <summary>
        /// Gets or sets the title of the asset.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the description of the asset.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the number of points the asset is worth.
        /// </summary>
        public int Points { get; internal set; }

        /// <summary>
        /// Gets the name of the badge for the asset.
        /// </summary>
        public string BadgeName { get; internal set; }

        /// <summary>
        /// Gets the date/time the asset was first published.
        /// </summary>
        public DateTime Published { get; internal set; }

        /// <summary>
        /// Gets the date/time the asset was last modified.
        /// </summary>
        public DateTime LastModified { get; internal set; }

        /// <summary>
        /// Gets whether or not the asset has been published but not promoted to Core.
        /// </summary>
        public virtual bool IsUnofficial
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the script line associated to creating the asset.
        /// </summary>
        internal int SourceLine { get; set; }
    }
}
