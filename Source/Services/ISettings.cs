﻿using System.Collections.Generic;

namespace RATools.Services
{
    public interface ISettings
    {
        /// <summary>
        /// Gets the directories to search for cached RA data.
        /// </summary>
        IEnumerable<string> EmulatorDirectories { get; }

        /// <summary>
        /// Gets the name of the user.
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// Gets the API key for the user.
        /// </summary>
        string ApiKey { get; }

        /// <summary>
        /// Gets the DoRequest token for the user.
        /// </summary>
        string DoRequestToken { get; }

        /// <summary>
        /// Gets the web Cookie for the user.
        /// </summary>
        string Cookie { get; }

        /// <summary>
        /// Gets or sets a value indicating whether values should be displayed in hexadecimal.
        /// </summary>
        bool HexValues { get; set; }

        /// <summary>
        /// Gets the directory where data for analysis should be stored.
        /// </summary>
        string DumpDirectory { get; }
    }
}
