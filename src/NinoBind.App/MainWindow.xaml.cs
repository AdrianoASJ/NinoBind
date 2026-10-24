using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NinoBind.Core.Configuration;
using NinoBind.Core.Profiles;
using NinoBind.Hosting;
using NinoBind.Input;
using NinoBind.Views;

namespace NinoBind;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly DesktopInputSession _input = new();
    private readonly BindingController _bindings;
    private readonly OverviewView _overview;
    private readonly SettingsView _settings;
    private readonly EmergencyHotkey _emergency = new();
    private readonly EngineHotkey _engineHotkey = new();
    private DevicesView? _devices;
    private BindingsView? _bindingsView;
    private MacrosView? _macrosView;
    private ProfilesView? _profilesView;
    private const int EngineCooldownSeconds = 2;
    private readonly System.Windows.Threading.DispatcherTimer _engineCooldown = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _cooldownRemaining;
    private nint _windowHandle;
    private EngineState _engine = EngineState.NotStarted;

    public MainWindow(AppHost host)
    {
        _host = host;
        _bindings = new BindingController(_input, host.Configuration.Document);
        InitializeComponent();
        _overview = new OverviewView(host);
        _settings = new SettingsView(host, SettingsChanged);
        SectionHost.Content = _overview;
        _tray = new TrayIcon(ShowFromTray, ToggleEngineFromTray, () => EmergencyStop(), ExitApplication);
        SourceInitialized += (_, _) => AttachInput();
        _bindings.MacrosChanged += _ => Dispatcher.BeginInvoke(UpdateChrome);
        _engineCooldown.Tick += (_, _) =>
        {
            if (--_cooldownRemaining <= 0)
                _engineCooldown.Stop();
            UpdateChrome();
        };
        Closing += (_, args) =>
        {
            if (_exiting || !_host.Configuration.Document.Settings.CloseToTray)
                return;
            args.Cancel = true;
            Hide();
        };
        Closed += (_, _) =>
        {
            _bindings.ReleaseHeld();
            _emergency.Dispose();
            _engineHotkey.Dispose();
            _input.Dispose();
            _tray.Dispose();
            Application.Current.Shutdown();
        };
    }

    private readonly TrayIcon _tray;
    private bool _exiting;

    public void StartInTray()
    {
        _ = new WindowInteropHelper(this).EnsureHandle();
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        _ = Activate();
    }

    private void ExitApplication()
    {
        _exiting = true;
        Close();
    }

    private void ToggleEngineFromTray()
    {
        if (_engineCooldown.IsEnabled)
            return;
        if (!SetEngine(_engine != EngineState.Running))
            ShowFromTray();
    }

    private void EngineClicked(object sender, RoutedEventArgs e) => ToggleEngineFromHotkey();

    private void ToggleEngineFromHotkey()
    {
        if (_engineCooldown.IsEnabled)
            return;
        if (!SetEngine(_engine != EngineState.Running))
            StatusText.Text = _input.ObservationError ?? "O motor não mudou de estado.";
    }

    private void StartEngineCooldown()
    {
        _cooldownRemaining = EngineCooldownSeconds;
        _engineCooldown.Stop();
        _engineCooldown.Start();
    }

    private void SettingsChanged()
    {
        UpdateChrome();
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control)
            return;
        RadioButton? target = e.Key switch
        {
            System.Windows.Input.Key.D1 or System.Windows.Input.Key.NumPad1 => OverviewNav,
            System.Windows.Input.Key.D2 or System.Windows.Input.Key.NumPad2 => DevicesNav,
            System.Windows.Input.Key.D3 or System.Windows.Input.Key.NumPad3 => BindingsNav,
            System.Windows.Input.Key.D4 or System.Windows.Input.Key.NumPad4 => MacrosNav,
            System.Windows.Input.Key.D5 or System.Windows.Input.Key.NumPad5 => ProfilesNav,
            System.Windows.Input.Key.D6 or System.Windows.Input.Key.NumPad6 => SettingsNav,
            _ => null,
        };
        if (target is null)
            return;
        target.IsChecked = true;
        _ = target.Focus();
        e.Handled = true;
    }

    private void AttachInput()
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        if (source is null)
            return;
        _windowHandle = source.Handle;
        source.AddHook(WindowHook);
        _input.DevicesChanged += RefreshOverview;
        _input.RefreshDevices();
        _host.Log.Info($"devices {_input.Devices.Count}");
        if (_input.EnumerationError is not null)
            _host.Log.Error(_input.EnumerationError);
        if (!_emergency.Register(_windowHandle))
            _host.Log.Error("emergency hotkey unavailable");
        if (!_engineHotkey.Register(_windowHandle))
            _host.Log.Error("engine hotkey unavailable");
        if (_host.Configuration.Document.Settings.StartEngineOnLaunch && !SetEngine(true))
            _host.Log.Error("engine did not start on launch");
        RefreshOverview();
        UpdateChrome();
    }

    private nint WindowHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (_emergency.IsTrigger(message, wParam))
        {
            handled = true;
            EmergencyStop();
            return 0;
        }

        if (_engineHotkey.IsTrigger(message, wParam))
        {
            handled = true;
            ToggleEngineFromHotkey();
            return 0;
        }

        _input.ProcessWindowMessage(message, lParam);
        return 0;
    }

    private void EmergencyStop()
    {
        var stopped = _bindings.EmergencyStop();
        if (_engine == EngineState.Running)
            _ = SetEngine(false);
        _host.Log.Info($"emergency stop macros {stopped}");
        UpdateChrome();
    }

    private void RefreshOverview()
    {
        var detectionRan = _input.EnumerationError is null;
        _overview.Apply(OverviewSnapshot.From(
            _host.Configuration.Document,
            _engine,
            detectionRan,
            detectionRan ? _input.Devices.Count : 0));
    }

    private bool SetEngine(bool enabled)
    {
        if (_windowHandle == 0)
            return false;
        if (enabled)
        {
            _bindings.Use(_host.Configuration.Document);
            if (!_input.SetEngine(_windowHandle, true))
                return false;
            _engine = EngineState.Running;
            _host.Log.Info("engine on");
        }
        else
        {
            _bindings.ReleaseHeld();
            if (!_input.SetEngine(_windowHandle, false))
                return false;
            _engine = EngineState.Paused;
            _host.Log.Info("engine off");
        }

        StartEngineCooldown();
        UpdateChrome();
        RefreshOverview();
        return true;
    }

    private void UpdateChrome()
    {
        var running = _engine == EngineState.Running;
        var watching = _input.IsObserving;
        var macros = _bindings.RunningMacros;
        var parts = new List<string>
        {
            running ? "Motor ativo" : _engine == EngineState.Paused ? "Motor pausado" : "Motor não iniciado",
        };
        if (watching)
            parts.Add("observação ativa");
        if (macros > 0)
            parts.Add(macros == 1 ? "1 macro rodando" : $"{macros} macros rodando");
        if (!_emergency.IsRegistered && _windowHandle != 0)
            parts.Add($"{EmergencyHotkey.Label} indisponível");
        if (!_engineHotkey.IsRegistered && _windowHandle != 0)
            parts.Add($"{EngineHotkey.Label} indisponível");
        StatusText.Text = string.Join(" · ", parts);
        var coolingDown = _engineCooldown.IsEnabled;
        EngineButton.IsEnabled = !coolingDown && _windowHandle != 0;
        EngineButton.Content = coolingDown
            ? $"Aguarde {_cooldownRemaining} s"
            : running ? "Desativar motor" : "Ativar motor";
        EngineButton.Style = (Style)FindResource(running ? "ActionButton" : "PrimaryButton");
        _tray.Update(StatusText.Text, running, !coolingDown);
        StatusText.Foreground = macros > 0 || (!_emergency.IsRegistered || !_engineHotkey.IsRegistered) && _windowHandle != 0
            ? (Brush)FindResource("WarnBrush")
            : running || watching
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("MutedBrush");
    }

    private void SectionChanged(object sender, RoutedEventArgs e)
    {
        if (SectionHost is null || sender is not RadioButton button || button.Tag is not string section)
            return;

        SectionHost.Content = section switch
        {
            "overview" => _overview,
            "devices" => DevicesPage(),
            "bindings" => BindingsPage(),
            "macros" => MacrosPage(),
            "profiles" => ProfilesPage(),
            "settings" => _settings,
            _ => _overview,
        };
        if (section == "overview")
            RefreshOverview();
        else if (section == "bindings")
            _bindingsView?.Reload();
        else if (section == "macros")
            _macrosView?.Reload();
        else if (section == "profiles")
            _profilesView?.Reload();
    }

    private UIElement ProfilesPage() =>
        _profilesView ??= new ProfilesView(_host, ActivateProfile, () =>
        {
            _bindings.Use(_host.Configuration.Document);
            RefreshOverview();
            UpdateChrome();
        });

    private string? ActivateProfile(string profileId)
    {
        if (_host.Configuration.Document.ActiveProfileId == profileId)
            return "Esse perfil já está ativo.";
        _bindings.ReleaseHeld();
        if (!_host.TryApply(document => ProfileService.Activate(document, profileId), out var error))
            return error ?? "O perfil não foi ativado.";
        _bindings.Use(_host.Configuration.Document);
        _host.Log.Info("profile switched");
        RefreshOverview();
        UpdateChrome();
        return null;
    }

    private UIElement MacrosPage() =>
        _macrosView ??= new MacrosView(_host, _bindings, () =>
        {
            RefreshOverview();
            UpdateChrome();
        });

    private UIElement DevicesPage()
    {
        if (_windowHandle == 0)
            return new NoteView("Dispositivos", "A janela ainda não está pronta para consultar o Raw Input.");

        return _devices ??= new DevicesView(_input, _windowHandle, _ => UpdateChrome());
    }

    private UIElement BindingsPage()
    {
        if (_windowHandle == 0)
            return new NoteView("Bindings", "A janela ainda não está pronta para capturar entradas.");

        return _bindingsView ??= new BindingsView(
            _host,
            _input,
            _bindings,
            _windowHandle,
            () =>
            {
                RefreshOverview();
                UpdateChrome();
            });
    }
}
