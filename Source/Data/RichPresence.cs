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
                _script = value;
                Description = string.Format("{0}/{1} characters", _script.Length, ScriptMaxLength);
            }
        }
        private string _script;
    }
}
