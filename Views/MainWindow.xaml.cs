using Jamiras.Components;
using Jamiras.Controls;
using Jamiras.Services;
using RATools.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace RATools.Views
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

            dialogService.RegisterDialogHandler(typeof(DumpPublishedDialogViewModel), vm => new OkCancelView(new DumpPublishedDialog()));
            dialogService.RegisterDialogHandler(typeof(GameStatsViewModel), vm => new GameStatsDialog());
            dialogService.RegisterDialogHandler(typeof(OpenTicketsViewModel), vm => new OpenTicketsDialog());
            dialogService.RegisterDialogHandler(typeof(AboutDialogViewModel), vm => new OkCancelView(new AboutDialog()));

            var viewModel = new MainWindowViewModel();
            viewModel.Initialize();
            DataContext = viewModel;

            InputBindings.Add(new KeyBinding(viewModel.RefreshCurrentCommand, new KeyGesture(Key.F5)));

            base.OnInitialized(e);
        }
    }
}
