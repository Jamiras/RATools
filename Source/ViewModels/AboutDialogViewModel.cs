using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using System;
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
                foreach (AssemblyMetadataAttribute metadata in Attribute.GetCustomAttributes(Assembly.GetExecutingAssembly(), typeof(AssemblyMetadataAttribute), false))
                {
                    if (metadata.Key == "RepositoryUrl")
                        return metadata.Value;
                }

                return "";
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
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(SourceLink);
        }
    }
}
