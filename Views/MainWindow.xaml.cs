using Jamiras.Components;
using Jamiras.Controls;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.ViewModels;
using System;
using System.ComponentModel;
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

            var windowSettingsRepository = ServiceRepository.Instance.FindService<IWindowSettingsRepository>();
            windowSettingsRepository.RestoreSettings(this);

            dialogService.RegisterDialogHandler(typeof(NewScriptDialogViewModel), vm => new NewScriptDialog());
            dialogService.RegisterDialogHandler(typeof(UpdateLocalViewModel), vm => new OkCancelView(new UpdateLocalDialog()));
            dialogService.RegisterDialogHandler(typeof(GameStatsViewModel), vm => new GameStatsDialog());
            dialogService.RegisterDialogHandler(typeof(OpenTicketsViewModel), vm => new OpenTicketsDialog());
            dialogService.RegisterDialogHandler(typeof(AboutDialogViewModel), vm => new OkCancelView(new AboutDialog()));

            dialogService.RegisterDialogHandler(typeof(MessageBoxViewModel), CreateMessageBoxView);

            var viewModel = new MainWindowViewModel();
            viewModel.Initialize();
            DataContext = viewModel;

            base.OnInitialized(e);
        }

        private FrameworkElement CreateMessageBoxView(DialogViewModelBase viewModel)
        {
            var textBlock = new FormattedTextBlock();
            textBlock.Margin = new Thickness(4);
            textBlock.SetBinding(FormattedTextBlock.TextProperty, "Message");
            textBlock.TextWrapping = TextWrapping.Wrap;
            return new OkCancelView(textBlock);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel != null && !viewModel.CloseEditor())
                e.Cancel = true;

            if (!e.Cancel)
            {
                var windowSettingsRepository = ServiceRepository.Instance.FindService<IWindowSettingsRepository>();
                windowSettingsRepository.RememberSettings(this);
            }

            base.OnClosing(e);
        }
    }
}
