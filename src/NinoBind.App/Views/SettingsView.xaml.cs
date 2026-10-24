using System.Windows;
using System.Windows.Controls;
using NinoBind.Hosting;
using NinoBind.Input;

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
        LanguageText.Text = settings.Language;
        PathText.Text = host.ConfigurationPath;
        LogText.Text = host.LogPath + " (registra estados e contagens, nunca teclas ou textos)";
        EmergencyText.Text = $"{EmergencyHotkey.Label} para as macros, solta as entradas sintetizadas e pausa o motor, mesmo com outra janela em foco.";
        EngineHotkeyText.Text = $"{EngineHotkey.Label} liga e desliga o motor, mesmo com outra janela em foco. Depois de cada troca, o atalho e o botão ficam bloqueados por 2 segundos.";
        Loaded += (_, _) => ShowStartup();
    }

    private void ShowStartup()
    {
        var status = StartupRegistration.Read();
        StartupBox.IsChecked = status.State is StartupState.On or StartupState.OtherPath;
        StartupBox.IsEnabled = status.State != StartupState.Unknown;
        StartupText.Text = status.State switch
        {
            StartupState.On => "Registrado em HKCU\\...\\Run para este executável.",
            StartupState.OtherPath => $"Registrado para outro caminho: {status.RegisteredCommand}. Desmarque e marque de novo para usar este executável.",
            StartupState.Unknown => "Não foi possível ler o registro. " + status.Error,
            _ => "Desligado. Nada foi gravado no registro.",
        };
    }

    private void StartupClicked(object sender, RoutedEventArgs e)
    {
        var error = StartupRegistration.Set(StartupBox.IsChecked == true);
        StatusText.Text = error is null ? "" : "O registro não foi alterado. " + error;
        _host.Log.Info(error is null ? $"startup {(StartupBox.IsChecked == true ? "on" : "off")}" : "startup change failed");
        ShowStartup();
    }

    private void TrayClicked(object sender, RoutedEventArgs e) =>
        Apply(enabled => _host.Configuration.Document.Settings.CloseToTray = enabled, TrayBox);

    private void EngineClicked(object sender, RoutedEventArgs e) =>
        Apply(enabled => _host.Configuration.Document.Settings.StartEngineOnLaunch = enabled, EngineBox);

    private void Apply(Action<bool> set, CheckBox box)
    {
        var enabled = box.IsChecked == true;
        if (_host.TryApply(_ => { set(enabled); return null; }, out var error))
        {
            StatusText.Text = "Salvo.";
            _changed();
            return;
        }

        box.IsChecked = !enabled;
        StatusText.Text = error ?? "A alteração não foi salva.";
    }
}
