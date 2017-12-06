using Jamiras.Components;
using Jamiras.IO;
using Jamiras.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RATools.Services
{
    [Export(typeof(ISettings))]
    internal class Settings : ISettings
    {
        public Settings()
            : this(ServiceRepository.Instance.FindService<IPersistantDataRepository>())
        {

        }

        internal Settings(IPersistantDataRepository persistance)
        {
            _persistance = persistance;

            var hexValues = persistance.GetValue("HexValues");
            _hexValues = (hexValues == "1");

            DataDirectories = new string[0];
            UserName = "RATools";

            var file = new IniFile("RATools.ini");
            try
            {
                var values = file.Read();
                DataDirectories = values["RACacheDirectory"].Split(';');

                string user;
                if (values.TryGetValue("User", out user) && user.Length > 0)
                    UserName = user;
            }
            catch (FileNotFoundException)
            {
            }
        }

        private readonly IPersistantDataRepository _persistance;

        public IEnumerable<string> DataDirectories { get; private set; }

        public string UserName { get; private set; }

        public bool HexValues
        {
            get { return _hexValues; }
            set
            {
                if (_hexValues != value)
                {
                    _hexValues = value;
                    _persistance.SetValue("HexValues", value ? "1" : "0");
                }
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _hexValues;
    }
}
