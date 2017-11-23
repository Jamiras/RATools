using System;
using System.Windows;
using RATools.ViewModels;
using Jamiras.Components;
using Jamiras.Services;
using Jamiras.Controls;

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
            CoreServices.RegisterServices();
            var dialogService = ServiceRepository.Instance.FindService<IDialogService>();
            dialogService.MainWindow = this;

            dialogService.RegisterDialogHandler(typeof(GameStatsViewModel), vm => new OkCancelView(new GameStatsDialog()));
            dialogService.RegisterDialogHandler(typeof(OpenTicketsViewModel), vm => new OkCancelView(new OpenTicketsDialog()));

            var viewModel = new MainWindowViewModel();
            viewModel.Initialize();
            DataContext = viewModel;

            base.OnInitialized(e);
        }
    }
}
