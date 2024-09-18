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

            MouseDown += MainWindow_MouseDown;
        }

        private void MainWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            GameViewModel game;
            switch (e.ChangedButton)
            {
                case MouseButton.XButton1:
                    game = ((MainWindowViewModel)DataContext).Game;
                    if (game != null)
                        game.NavigateBack();
                    break;
                case MouseButton.XButton2:
                    game = ((MainWindowViewModel)DataContext).Game;
                    if (game != null)
                        game.NavigateForward();
                    break;
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            CoreServices.RegisterServices();
            UIServices.RegisterServices();

            var exceptionDispatcher = ServiceRepository.Instance.FindService<IExceptionDispatcher>();
            exceptionDispatcher.SetExceptionHandler(UnhandledExceptionHandler);

            var dialogService = ServiceRepository.Instance.FindService<IDialogService>();
            dialogService.MainWindow = this;
            dialogService.DefaultWindowTitle = "RA Tools";

            var windowSettingsRepository = ServiceRepository.Instance.FindService<IWindowSettingsRepository>();
            windowSettingsRepository.RestoreSettings(this);

            dialogService.RegisterDialogHandler(typeof(NewScriptDialogViewModel), vm => new NewScriptDialog());
            dialogService.RegisterDialogHandler(typeof(OptionsDialogViewModel), vm => new OkCancelView(new OptionsDialog()));
            dialogService.RegisterDialogHandler(typeof(UpdateLocalViewModel), vm => new OkCancelView(new UpdateLocalDialog()));
            dialogService.RegisterDialogHandler(typeof(ViewAchievementsViewModel), vm => new OkCancelView(new ViewAchievementsDialog()));
            dialogService.RegisterDialogHandler(typeof(GameStatsViewModel), vm => new GameStatsDialog());
            dialogService.RegisterDialogHandler(typeof(GameStatsViewModel.UserHistoryViewModel), vm => new UserHistoryDialog());
            dialogService.RegisterDialogHandler(typeof(GameProgressionViewModel), vm => new GameProgressionDialog());
            dialogService.RegisterDialogHandler(typeof(OpenTicketsViewModel), vm => new OpenTicketsDialog());
            dialogService.RegisterDialogHandler(typeof(AboutDialogViewModel), vm => new OkCancelView(new AboutDialog()));
            dialogService.RegisterDialogHandler(typeof(ConditionsAnalyzerViewModel), vm => new ConditionsAnalyzerDialog());
            dialogService.RegisterDialogHandler(typeof(MasteryViewModel), vm => new MasteryDialog());
            dialogService.RegisterDialogHandler(typeof(UserMasteriesViewModel), vm => new UserMasteriesDialog());
            dialogService.RegisterDialogHandler(typeof(UnlockDistanceViewModel), vm => new UnlockDistanceDialog());

            var viewModel = new MainWindowViewModel();
            viewModel.Initialize();
            DataContext = viewModel;

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                Dispatcher.InvokeAsync(() => {
                    viewModel.DragDropScriptCommand.Execute(new string[] { args[1] });
                });
            }

            base.OnInitialized(e);
        }

        private static void UnhandledExceptionHandler(object sender, DispatchExceptionEventArgs e)
        {
            var innerException = e.Exception;
            while (innerException.InnerException != null)
                innerException = innerException.InnerException;
            var stackTrace = innerException.StackTrace;

            try
            {
                var logService = ServiceRepository.Instance.FindService<ILogService>();
                var logger = logService.GetLogger("Jamiras.Core");
                logger.WriteError(innerException.Message + "\n" + stackTrace);
            }
            catch
            {
                // ignore exception trying to log exception
            }

            string title = "RA Tools - ";
            if (e.IsUnhandled)
                title += "Unhandled ";
            title += innerException.GetType().Name;

            string detail = "More detail may be in the log file.";
            if (e.ShouldTerminate)
                detail += "\n\nThe application will now terminate.";

            TaskDialogViewModel.ShowErrorMessage(innerException.Message, detail, title);
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
