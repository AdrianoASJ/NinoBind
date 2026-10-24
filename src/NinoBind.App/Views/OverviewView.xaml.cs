using System.Windows.Controls;
using NinoBind.Core.Configuration;
using NinoBind.Hosting;

namespace NinoBind.Views;

public partial class OverviewView : UserControl
{
    public OverviewView(AppHost host)
    {
        InitializeComponent();
        Apply(host.Overview);

        if (host.Configuration.Status == ConfigurationLoadStatus.Invalid)
        {
            WarningBanner.Visibility = System.Windows.Visibility.Visible;
            WarningText.Text =
                "O arquivo de configuração está inválido e foi mantido como está. Os números abaixo são um estado vazio em memória, não o conteúdo desse arquivo. " +
                host.Configuration.Error;
        }
    }

    public void Apply(OverviewSnapshot snapshot)
    {
        EngineText.Text = snapshot.Engine switch
        {
            EngineState.Running => "O motor de remapeamento está ativo.",
            EngineState.Paused => "O motor de remapeamento está pausado.",
            _ => "O motor de remapeamento não foi iniciado.",
        };
        DevicesValue.Text = snapshot.DeviceDetectionRan
            ? snapshot.DetectedDeviceCount.ToString()
            : "Ainda não detectados";
        ProfileValue.Text = snapshot.ActiveProfileName;
        BindingsValue.Text = snapshot.ActiveBindingCount.ToString();
        MacrosValue.Text = snapshot.MacroCount.ToString();
    }
}
