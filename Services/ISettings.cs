using System.Collections.Generic;

namespace RATools.Services
{
    public interface ISettings
    {
        /// <summary>
        /// Gets the directories to search for cached RA data.
        /// </summary>
        IEnumerable<string> DataDirectories { get; }

        /// <summary>
        /// Gets the name of the user.
        /// </summary>
        string UserName { get; }
    }
}
