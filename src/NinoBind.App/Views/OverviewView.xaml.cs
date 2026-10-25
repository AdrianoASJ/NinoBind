using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NinoBind.Core.Configuration;
using NinoBind.Hosting;

namespace NinoBind.Views;

public partial class OverviewView : UserControl
{
    private readonly Action _toggleEngine;
    private readonly Action<string> _navigate;

    public OverviewView(AppHost host, Action toggleEngine, Action<string> navigate)
    {
        _toggleEngine = toggleEngine;
        _navigate = navigate;
        InitializeComponent();
        ActivityList.ItemsSource = ActivityFeed.Current.Items;
        Apply(host.Overview, 0, 0, 0);
        SetEngine(host.Overview.Engine);

        if (host.Configuration.Status == ConfigurationLoadStatus.Invalid)
        {
            WarningBanner.Visibility = Visibility.Visible;
            WarningText.Text =
                "O arquivo de configuração está inválido e foi mantido como está. Os números abaixo são um estado vazio em memória, não o conteúdo desse arquivo. " +
                host.Configuration.Error;
        }
    }

    public void Apply(OverviewSnapshot snapshot, int mice, int keys, int runningMacros)
    {
        DevicesValue.Text = snapshot.DeviceDetectionRan
            ? snapshot.DetectedDeviceCount.ToString()
            : "0";
        DevicesMeta.Text = snapshot.DeviceDetectionRan
            ? $"{mice} {(mice == 1 ? "mouse" : "mouses")} · {keys} {(keys == 1 ? "teclado" : "teclados")}"
            : "Ainda não detectados";
        ProfileValue.Text = string.IsNullOrEmpty(snapshot.ActiveProfileName) ? "—" : snapshot.ActiveProfileName;
        BindingsValue.Text = snapshot.ActiveBindingCount.ToString();
        BindingsMeta.Text = snapshot.TotalBindingCount == 1
            ? "de 1 binding"
            : $"de {snapshot.TotalBindingCount} bindings";
        MacrosValue.Text = snapshot.MacroCount.ToString();
        MacrosMeta.Text = runningMacros == 0
            ? "Nenhuma rodando"
            : runningMacros == 1 ? "1 rodando" : $"{runningMacros} rodando";
        SetEngine(snapshot.Engine);
    }

    public void SetEngine(EngineState engine)
    {
        var running = engine == EngineState.Running;
        EngineTitle.Text = running ? "Motor ativo" : "Motor parado";
        EngineTitle.Foreground = (Brush)FindResource(running ? "SuccessBrush" : "DangerBrush");
        EngineCard.Background = (Brush)FindResource(running ? "EngineSuccessFill" : "EngineDangerFill");
        EngineCard.BorderBrush = (Brush)FindResource(running ? "SuccessBrush" : "DangerLineBrush");
        EngineActionLabel.Text = running ? "Parar motor" : "Ativar motor";
        EngineAction.Style = (Style)FindResource(running ? "DangerButton" : "PriButton");
    }

    private void EngineClicked(object sender, RoutedEventArgs e) => _toggleEngine();

    private void GoDevices(object sender, RoutedEventArgs e) => _navigate("devices");

    private void GoProfiles(object sender, RoutedEventArgs e) => _navigate("profiles");

    private void GoBindings(object sender, RoutedEventArgs e) => _navigate("bindings");

    private void GoMacros(object sender, RoutedEventArgs e) => _navigate("macros");
}
