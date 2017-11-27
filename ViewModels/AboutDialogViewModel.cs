using Jamiras.Commands;
using Jamiras.ViewModels;
using System;
using System.Diagnostics;
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
                return String.Format("{0} {1}", GetAssemblyAttribute<AssemblyTitleAttribute>().Title, Assembly.GetExecutingAssembly().GetName().Version.ToString());
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
    }
}
