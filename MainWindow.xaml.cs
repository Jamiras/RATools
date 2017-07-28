using System;
using System.Windows;
using RATools.ViewModels;
using Jamiras.Components;
using Jamiras.Services;

namespace RATools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            DataContext = new MainWindowViewModel();

            CoreServices.RegisterServices();
            var dialogService = ServiceRepository.Instance.FindService<IDialogService>();
            dialogService.MainWindow = this;

            base.OnInitialized(e);
        }
    }
}
