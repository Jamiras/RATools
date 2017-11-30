using Jamiras.Components;
using Jamiras.IO;
using System.Collections.Generic;
using System.IO;

namespace RATools.Services
{
    [Export(typeof(ISettings))]
    internal class Settings : ISettings
    {
        public Settings()
        {
            DataDirectories = new string[0];
            UserName = "RATools";

            var file = new IniFile("RATools.ini");
            try
            {
                var values = file.Read();
                DataDirectories = values["RACacheDirectory"].Split(';');

                string user;
                if (values.TryGetValue("User", out user))
                    UserName = user;
            }
            catch (FileNotFoundException)
            {
            }
        }

        public IEnumerable<string> DataDirectories { get; private set; }

        public string UserName { get; private set; }
    }
}
