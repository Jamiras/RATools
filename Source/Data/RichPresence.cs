using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Defines a rich presence script.
    /// </summary>
    [DebuggerDisplay("Rich Presence")]
    public class RichPresence : AssetBase
    {
        public RichPresence()
        {
            Title = "Rich Presence";
        }

        /// <summary>
        /// Gets the maximum number of characters allowed in a rich presence script.
        /// </summary>
        /// <remarks>
        /// The database field actually supports 65535 bytes, but the field on the
        /// webpage limits submissions to 60000 characters so it doesn't have to
        /// convert characters to bytes.
        /// </remarks>
        public const int ScriptMaxLength = 60000;

        public string Script 
        { 
            get { return _script; }
            set
            {
                // normalize to Windows line endings as they take more space and that's what's
                // probably going to be uploaded on the server when the user pastes into the
                // web site.
                if (!value.Contains('\r'))
                    value = value.Replace("\n", "\r\n");

                // ignore any leading/trailing whitespace. normally, the server value won't have
                // a trailing newline, but the generated script will.
                value = value.Trim();

                _script = value;
                Description = string.Format("{0}/{1} characters", _script.Length, ScriptMaxLength);
            }
        }
        private string _script;
    }
}
