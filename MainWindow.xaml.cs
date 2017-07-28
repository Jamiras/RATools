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
            var viewModel = new MainWindowViewModel();
            viewModel.Initialize();
            DataContext = viewModel;

            CoreServices.RegisterServices();
            var dialogService = ServiceRepository.Instance.FindService<IDialogService>();
            dialogService.MainWindow = this;

            base.OnInitialized(e);
        }
    }
}
