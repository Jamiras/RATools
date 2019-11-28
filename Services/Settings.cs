using Jamiras.Components;
using Jamiras.IO;
using Jamiras.Services;
using System;
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

            EmulatorDirectories = new List<string>();
            UserName = "RATools";

            var file = new IniFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RATools.ini"));
            try
            {
                var values = file.Read();

                string emulatorDirectories;
                if (values.TryGetValue("EmulatorDirectories", out emulatorDirectories))
                {
                    EmulatorDirectories = new List<string>(emulatorDirectories.Split(';'));
                }
                else if (values.TryGetValue("RACacheDirectory", out emulatorDirectories))
                {
                    foreach (var path in emulatorDirectories.Split(';'))
                    {
                        if (path.EndsWith("RACache\\Data", StringComparison.OrdinalIgnoreCase))
                            EmulatorDirectories.Add(path.Substring(0, path.Length - 13));
                        else
                            EmulatorDirectories.Add(path);
                    }
                }

                string user;
                if (values.TryGetValue("User", out user) && user.Length > 0)
                    UserName = user;

                string apiKey;
                if (values.TryGetValue("ApiKey", out apiKey) && apiKey.Length > 0)
                    ApiKey = apiKey;
            }
            catch (FileNotFoundException)
            {
            }
        }

        private readonly IPersistantDataRepository _persistance;

        public void Save()
        {
            var file = new IniFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RATools.ini"));
            IDictionary<string, string> values;
            try
            {
                values = file.Read();
            }
            catch (FileNotFoundException)
            {
                values = new Dictionary<string, string>();
            }

            values["User"] = UserName;

            if (String.IsNullOrEmpty(ApiKey))
                values.Remove("ApiKey");
            else
                values["ApiKey"] = ApiKey;

            if (EmulatorDirectories.Count == 0)
                values.Remove("EmulatorDirectories");
            else
                values["EmulatorDirectories"] = string.Join(";", EmulatorDirectories);

            values.Remove("RACacheDirectory");

            file.Write(values);
        }

        public IList<string> EmulatorDirectories { get; private set; }
        IEnumerable<string> ISettings.EmulatorDirectories
        {
            get { return EmulatorDirectories; }
        }

        public string UserName { get; set; }

        public string ApiKey { get; set; }

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
