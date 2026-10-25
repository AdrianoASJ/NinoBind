using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using NinoBind.Hosting;

namespace NinoBind.Views;

public partial class SettingsView : UserControl
{
    private readonly AppHost _host;
    private readonly Action _changed;

    public SettingsView(AppHost host, Action changed)
    {
        _host = host;
        _changed = changed;
        InitializeComponent();
        var settings = host.Configuration.Document.Settings;
        TrayBox.IsChecked = settings.CloseToTray;
        EngineBox.IsChecked = settings.StartEngineOnLaunch;
        TrayStateText.Text = OnOff(settings.CloseToTray);
        EngineStateText.Text = OnOff(settings.StartEngineOnLaunch);
        LanguageBox.Items.Add("Português (pt-BR)");
        LanguageBox.SelectedIndex = 0;
        PathText.Text = host.ConfigurationPath;
        LogText.Text = host.LogPath;
        Loaded += (_, _) => ShowStartup();
    }

    private void ShowStartup()
    {
        var status = StartupRegistration.Read();
        StartupBox.IsChecked = status.State is StartupState.On or StartupState.OtherPath;
        StartupBox.IsEnabled = status.State != StartupState.Unknown;
        StartupStateText.Text = OnOff(StartupBox.IsChecked == true);
        StartupText.Text = status.State switch
        {
            StartupState.On => "Abre minimizado na bandeja do Windows.",
            StartupState.OtherPath => $"Registrado para outro caminho. Desmarque e marque de novo para usar este executável.",
            StartupState.Unknown => "Não foi possível ler o registro. " + status.Error,
            _ => "Abre minimizado na bandeja do Windows.",
        };
    }

    private void StartupClicked(object sender, RoutedEventArgs e)
    {
        var error = StartupRegistration.Set(StartupBox.IsChecked == true);
        StatusText.Text = error is null ? "" : "O registro não foi alterado. " + error;
        _host.Log.Info(error is null ? $"startup {(StartupBox.IsChecked == true ? "on" : "off")}" : "startup change failed");
        ShowStartup();
    }

    private void TrayClicked(object sender, RoutedEventArgs e)
    {
        Apply(enabled => _host.Configuration.Document.Settings.CloseToTray = enabled, TrayBox);
        TrayStateText.Text = OnOff(TrayBox.IsChecked == true);
    }

    private void EngineClicked(object sender, RoutedEventArgs e)
    {
        Apply(enabled => _host.Configuration.Document.Settings.StartEngineOnLaunch = enabled, EngineBox);
        EngineStateText.Text = OnOff(EngineBox.IsChecked == true);
    }

    private void Apply(Action<bool> set, CheckBox box)
    {
        var enabled = box.IsChecked == true;
        if (_host.TryApply(_ => { set(enabled); return null; }, out var error))
        {
            StatusText.Text = "";
            _changed();
            return;
        }

        box.IsChecked = !enabled;
        StatusText.Text = error ?? "A alteração não foi salva.";
    }

    private void OpenConfigClicked(object sender, RoutedEventArgs e) => Reveal(_host.ConfigurationPath);

    private void OpenLogClicked(object sender, RoutedEventArgs e) => Reveal(_host.LogPath);

    private void CopyConfigClicked(object sender, RoutedEventArgs e) => Copy(_host.ConfigurationPath);

    private void CopyLogClicked(object sender, RoutedEventArgs e) => Copy(_host.LogPath);

    private void Copy(string path)
    {
        Clipboard.SetText(path);
        StatusText.Text = "Caminho copiado.";
    }

    private static void Reveal(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true,
        });
    }

    private static string OnOff(bool on) => on ? "Ligado" : "Desligado";
}
