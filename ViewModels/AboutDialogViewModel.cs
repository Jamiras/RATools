using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.ViewModels;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RATools.ViewModels
{
    public class AboutDialogViewModel : DialogViewModelBase
    {
        public AboutDialogViewModel()
        {
            DialogTitle = "About";
            SourceLinkCommand = new DelegateCommand(OpenSourceLink);
            CancelButtonText = null;

            var directories = new List<LookupItem>();
            foreach (var path in ServiceRepository.Instance.FindService<ISettings>().DataDirectories)
                directories.Add(new LookupItem(Directory.Exists(path) ? 1 : 0, path));

            DataDirectories = directories;
        }

        public string ProductVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                if (version.EndsWith(".0"))
                    version = version.Substring(0, version.Length - 2);
                return String.Format("{0} {1}", GetAssemblyAttribute<AssemblyTitleAttribute>().Title, version);
            }
        }

        public string BuildDate
        {
            get
            {
                return GetAssemblyAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            }
        }

        public string CopyrightMessage
        {
            get
            {
                return GetAssemblyAttribute<AssemblyCopyrightAttribute>().Copyright;
            }
        }

        public string SourceLink
        {
            get
            {
                return GetAssemblyAttribute<AssemblyProductAttribute>().Product;
            }
        }

        private static T GetAssemblyAttribute<T>()
            where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(T), false);
        }

        public CommandBase SourceLinkCommand { get; private set; }

        private void OpenSourceLink()
        {
            Process.Start(SourceLink);
        }

        public IEnumerable<LookupItem> DataDirectories { get; private set; }
    }
}
