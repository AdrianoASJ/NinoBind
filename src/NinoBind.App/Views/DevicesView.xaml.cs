using System.Windows;
using System.Windows.Controls;
using NinoBind.Input;

namespace NinoBind.Views;

public partial class DevicesView : UserControl
{
    private const int EventLimit = 40;
    private readonly DesktopInputSession _session;
    private readonly nint _window;
    private readonly Action<bool> _observationChanged;

    public DevicesView(DesktopInputSession session, nint window, Action<bool> observationChanged)
    {
        _session = session;
        _window = window;
        _observationChanged = observationChanged;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _session.InputObserved += OnInput;
            _session.DevicesChanged += ShowDevices;
            ShowDevices();
            ShowObservation();
        };
        Unloaded += (_, _) =>
        {
            _session.InputObserved -= OnInput;
            _session.DevicesChanged -= ShowDevices;
        };
    }

    private void ShowDevices()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ShowDevices);
            return;
        }

        DeviceList.Items.Clear();
        foreach (var device in _session.Devices)
        {
            var manufacturer = string.IsNullOrWhiteSpace(device.Manufacturer)
                ? "Fabricante não informado"
                : device.Manufacturer;
            var product = string.IsNullOrWhiteSpace(device.Product)
                ? "Nome de produto não informado"
                : device.Product;
            var path = string.IsNullOrWhiteSpace(device.DevicePath)
                ? "Identificador não informado"
                : device.DevicePath;
            var buttons = device.ButtonCount is int count
                ? $" A API informou {count} botões."
                : "";
            var kind = device.Kind == InputDeviceKind.Keyboard ? "Teclado" : "Mouse";
            DeviceList.Items.Add(new DeviceRow(kind, device.DisplayName, $"{manufacturer}. {product}.{buttons}", path));
        }

        EnumerationText.Text = _session.EnumerationError is null
            ? $"{_session.Devices.Count} dispositivos de teclado ou mouse."
            : "A enumeração falhou e a lista não foi inventada. " + _session.EnumerationError;
    }

    private void ShowObservation()
    {
        ObserveButton.Content = _session.IsObserving ? "Parar observação" : "Observar entradas";
        TestButton.IsEnabled = _session.IsObserving;
        ObserveState.Text = _session.IsObserving
            ? "Observação ligada. As teclas aparecem aqui e não são gravadas em arquivo."
            : "Observação desligada. Nenhuma tecla está sendo lida.";
        if (_session.ObservationError is not null)
            ObserveState.Text = _session.ObservationError;
        _observationChanged(_session.IsObserving);
    }

    private void RefreshClicked(object sender, RoutedEventArgs e) => _session.RefreshDevices();

    private void ObserveClicked(object sender, RoutedEventArgs e)
    {
        if (_session.IsObserving)
            _session.Stop();
        else
            _ = _session.TryStart(_window);
        ShowObservation();
    }

    private void TestClicked(object sender, RoutedEventArgs e)
    {
        var sent = _session.SendTestKey();
        TestResult.Text = sent == 2
            ? "F13 foi enviado com a marca do NinoBind. Se a observação estiver ligada, a tecla deve aparecer como eco ignorado, sem virar um binding."
            : $"O Windows aceitou {sent} de 2 eventos de teste.";
    }

    private void OnInput(ObservedInput input)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnInput(input));
            return;
        }

        var device = input.Origin == InputOrigin.IgnoredEcho
            ? "eco do NinoBind, ignorado"
            : DeviceLabel(input.DeviceHandle);
        var edge = input.Kind switch
        {
            InputEventKind.KeyDown => "tecla pressionada",
            InputEventKind.KeyUp => "tecla liberada",
            InputEventKind.MouseDown => "botão pressionado",
            _ => "botão liberado",
        };
        EventList.Items.Insert(0, $"{edge}: {input.Description} — {device}");
        while (EventList.Items.Count > EventLimit)
            EventList.Items.RemoveAt(EventList.Items.Count - 1);
    }

    private string DeviceLabel(nint handle)
    {
        if (handle == 0)
            return "aparelho não separado";
        var device = _session.Devices.FirstOrDefault(item => item.Handle == handle);
        return device?.DisplayName ?? "aparelho não separado";
    }

    private sealed record DeviceRow(string Kind, string Name, string Detail, string Path);
}
