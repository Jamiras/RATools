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
        public int Id { get; set; }

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
        public int Points { get; set; }

        /// <summary>
        /// Gets the name of the badge for the asset.
        /// </summary>
        public string BadgeName { get; set; }

        /// <summary>
        /// Gets the date/time the asset was first published.
        /// </summary>
        public DateTime Published { get; set; }

        /// <summary>
        /// Gets the date/time the asset was last modified.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets whether or not the asset has been published but not promoted to Core.
        /// </summary>
        public virtual bool IsUnofficial
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the first unique identifier reserved for locally generated assets.
        /// </summary>
        public static readonly int FirstLocalId = 111000001;

        /// <summary>
        /// Gets the unique identifier of the game that owns this assset.
        /// </summary>
        public int OwnerGameId { get; set; }

        /// <summary>
        /// Gets the unique identifier of the achievement set that owns this asset.
        /// </summary>
        public int OwnerSetId { get; set; }
    }
}
