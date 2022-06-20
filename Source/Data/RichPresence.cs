using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Defines a rich presence script.
    /// </summary>
    [DebuggerDisplay("Rich Presence")]
    public class RichPresence : AssetBase
    {
        internal RichPresence()
        {
            Title = "Rich Presence";
        }

        public static int ScriptMaxLength = 65535;

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

                _script = value;
                Description = string.Format("{0}/{1} characters", _script.Length, ScriptMaxLength);
            }
        }
        private string _script;
    }
}
