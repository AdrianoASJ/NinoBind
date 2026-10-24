using System.Windows;
using NinoBind.Hosting;

namespace NinoBind;

public partial class App : Application
{
    private const string InstanceName = @"Local\NinoBind.SingleInstance";
    private const string ShowSignalName = @"Local\NinoBind.ShowWindow";
    private Mutex? _instance;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(initiallyOwned: true, InstanceName, out var createdNew);
        if (!createdNew)
        {
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var signal))
            {
                using (signal)
                    signal.Set();
            }

            _instance.Dispose();
            _instance = null;
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _host?.Log.Error($"unhandled {args.Exception.GetType().Name}: {args.Exception.Message}");
            var where = _host is null ? "" : $"\n\nDetalhes no log: {_host.LogPath}";
            MessageBox.Show(
                $"O NinoBind encontrou um erro e vai fechar. As teclas sintetizadas serão soltas.\n\n{args.Exception.Message}{where}",
                "NinoBind",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        _host = AppHost.Start();
        var startHidden = e.Args.Contains(StartupRegistration.TrayArgument, StringComparer.OrdinalIgnoreCase);
        var window = new MainWindow(_host);
        MainWindow = window;
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(
            _showSignal,
            (_, _) => Dispatcher.BeginInvoke(window.ShowFromTray),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        if (startHidden)
            window.StartInTray();
        else
            window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showSignal?.Dispose();
        if (_instance is not null)
        {
            _instance.ReleaseMutex();
            _instance.Dispose();
        }

        base.OnExit(e);
    }
}
