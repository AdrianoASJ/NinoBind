using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NinoBind.Hosting;
using NinoBind.Input;
using InputDevice = NinoBind.Input.InputDevice;

namespace NinoBind.Views;

public partial class DevicesView : UserControl
{
    private const int EventLimit = 40;
    private static readonly TimeSpan ReceivingWindow = TimeSpan.FromSeconds(3);
    private readonly AppHost _host;
    private readonly DesktopInputSession _session;
    private readonly nint _window;
    private readonly Action<bool> _observationChanged;
    private readonly Dictionary<nint, DateTime> _lastSeen = [];
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly HashSet<string> _expanded = [];
    private string? _editingKey;

    public DevicesView(AppHost host, DesktopInputSession session, nint window, Action<bool> observationChanged)
    {
        _host = host;
        _session = session;
        _window = window;
        _observationChanged = observationChanged;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _session.InputObserved += OnInput;
            _session.DevicesChanged += ShowDevices;
            _pulse.Tick += (_, _) =>
            {
                if (_editingKey is null)
                    ShowDevices();
            };
            _pulse.Start();
            ShowDevices();
            ShowObservation();
        };
        Unloaded += (_, _) =>
        {
            _session.InputObserved -= OnInput;
            _session.DevicesChanged -= ShowDevices;
            _pulse.Stop();
        };
    }

    private void ShowDevices()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ShowDevices);
            return;
        }

        var mice = _session.Devices.Where(device => device.Kind == InputDeviceKind.Mouse).ToList();
        var keys = _session.Devices.Where(device => device.Kind == InputDeviceKind.Keyboard).ToList();
        MouseCount.Text = mice.Count.ToString();
        KeyboardCount.Text = keys.Count.ToString();
        CountText.Text = _session.EnumerationError is null
            ? $"{_session.Devices.Count} dispositivos conectados"
            : "A enumeração falhou e a lista não foi inventada. " + _session.EnumerationError;
        FillGroup(MouseHost, mice);
        FillGroup(KeyboardHost, keys);
    }

    private void FillGroup(Grid host, List<InputDevice> devices)
    {
        host.Children.Clear();
        host.ColumnDefinitions.Clear();
        host.RowDefinitions.Clear();
        host.ColumnDefinitions.Add(new ColumnDefinition());
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        host.ColumnDefinitions.Add(new ColumnDefinition());
        var rows = Math.Max(1, (devices.Count + 1) / 2);
        for (var i = 0; i < rows; i++)
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < devices.Count; i++)
        {
            var card = DeviceCard(devices[i]);
            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2 == 0 ? 0 : 2);
            host.Children.Add(card);
        }
    }

    private UIElement DeviceCard(InputDevice device)
    {
        var key = DeviceLabels.KeyOf(device);
        var name = DeviceLabels.Friendly(device, _session.Devices, _host.Configuration.Document.Settings.DeviceNicknames);
        var receiving = _lastSeen.TryGetValue(device.Handle, out var seen) && DateTime.UtcNow - seen < ReceivingWindow;
        var card = new Border
        {
            Style = (Style)FindResource("CardBorder"),
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = (Brush)FindResource(receiving ? "GoldBrush" : "CardLineBrush"),
        };
        var root = new StackPanel();
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var well = new Border { Style = (Style)FindResource("IconWell"), Margin = new Thickness(0, 0, 12, 0) };
        well.Child = new TextBlock
        {
            Style = (Style)FindResource("Glyph"),
            Text = device.Kind == InputDeviceKind.Mouse ? "\uE962" : "\uE92E",
        };
        header.Children.Add(well);

        var identity = new StackPanel();
        Grid.SetColumn(identity, 1);
        if (_editingKey == key)
        {
            var box = new TextBox
            {
                Text = name,
                MaxLength = 60,
            };
            System.Windows.Automation.AutomationProperties.SetName(box, "Apelido do dispositivo");
            box.LostFocus += (_, _) => SaveNickname(device, box.Text);
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter)
                    return;
                SaveNickname(device, box.Text);
                e.Handled = true;
            };
            identity.Children.Add(box);
            Dispatcher.BeginInvoke(() =>
            {
                _ = box.Focus();
                box.SelectAll();
            }, DispatcherPriority.Input);
        }
        else
        {
            var nameRow = new Grid();
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nameText = new TextBlock
            {
                Text = name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
            nameRow.Children.Add(nameText);
            var edit = new Button
            {
                Style = (Style)FindResource("IconButton"),
                Content = "\uE70F",
                ToolTip = "Editar apelido",
                Margin = new Thickness(4, 0, 0, 0),
            };
            System.Windows.Automation.AutomationProperties.SetName(edit, "Editar apelido");
            Grid.SetColumn(edit, 1);
            edit.Click += (_, _) =>
            {
                _editingKey = key;
                ShowDevices();
            };
            nameRow.Children.Add(edit);
            identity.Children.Add(nameRow);
        }

        identity.Children.Add(new TextBlock
        {
            Text = DeviceMeta(device),
            Style = (Style)FindResource("MetaText"),
            Margin = new Thickness(0, 4, 0, 0),
        });
        header.Children.Add(identity);

        var badge = new Border
        {
            Style = (Style)FindResource(receiving ? "BadgeSuccess" : "BadgeNeutral"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(badge, 2);
        badge.Child = new TextBlock
        {
            Text = receiving ? "Recebendo" : "Inativo",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource(receiving ? "SuccessBrush" : "NeutralBrush"),
        };
        header.Children.Add(badge);
        root.Children.Add(header);

        var expanded = _expanded.Contains(key);
        var toggle = new Button
        {
            Content = expanded ? "Detalhes técnicos ▾" : "Detalhes técnicos ▸",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(54, 10, 0, 0),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Cursor = Cursors.Hand,
            FocusVisualStyle = (Style)FindResource("FocusRing"),
        };
        System.Windows.Automation.AutomationProperties.SetName(toggle, "Detalhes técnicos");
        toggle.Template = ButtonTextTemplate();
        toggle.Click += (_, _) =>
        {
            if (!_expanded.Add(key))
                _expanded.Remove(key);
            ShowDevices();
        };
        root.Children.Add(toggle);

        if (expanded)
        {
            var details = new StackPanel { Margin = new Thickness(54, 8, 0, 0) };
            if (!string.IsNullOrWhiteSpace(device.Manufacturer))
                details.Children.Add(DetailLine("Fabricante", device.Manufacturer));
            if (!string.IsNullOrWhiteSpace(device.Product))
                details.Children.Add(DetailLine("Produto", device.Product));
            details.Children.Add(DetailLine("Caminho", string.IsNullOrWhiteSpace(device.DevicePath) ? "Não informado" : device.DevicePath));
            if (HidPathIdentity.TryReadVidPid(device.DevicePath) is { } ids)
                details.Children.Add(DetailLine("VID/PID", $"{ids.VendorId:X4}/{ids.ProductId:X4}"));
            details.Children.Add(DetailLine("Handle", device.Handle.ToString()));
            root.Children.Add(details);
        }

        card.Child = root;
        return card;
    }

    private ControlTemplate ButtonTextTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(ContentPresenter));
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        template.VisualTree = factory;
        return template;
    }

    private TextBlock DetailLine(string label, string value) => new()
    {
        Text = $"{label}: {value}",
        Style = (Style)FindResource("MonoText"),
        Margin = new Thickness(0, 0, 0, 4),
        TextWrapping = TextWrapping.Wrap,
    };

    private static string DeviceMeta(InputDevice device)
    {
        var parts = new List<string>();
        if (device.ButtonCount is int count)
            parts.Add(count == 1 ? "1 botão" : $"{count} botões");
        if (!string.IsNullOrEmpty(device.DevicePath)
            && (device.DevicePath.Contains("USB", StringComparison.OrdinalIgnoreCase)
                || device.DevicePath.Contains("HID", StringComparison.OrdinalIgnoreCase)))
            parts.Add("USB");
        return parts.Count == 0 ? "Conectado" : string.Join(" · ", parts);
    }

    private void SaveNickname(InputDevice device, string name)
    {
        _editingKey = null;
        var key = DeviceLabels.KeyOf(device);
        var fallback = DeviceLabels.Friendly(device, _session.Devices, new Dictionary<string, string>());
        var nicknames = _host.Configuration.Document.Settings.DeviceNicknames;
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == fallback)
            nicknames.Remove(key);
        else
            nicknames[key] = trimmed;
        _ = _host.TrySave(out _);
        ShowDevices();
    }

    private void ShowObservation()
    {
        ObserveButton.Content = _session.IsObserving ? "Parar observação" : "Observar entradas";
        TestButton.Visibility = _session.IsObserving ? Visibility.Visible : Visibility.Collapsed;
        TestButton.IsEnabled = _session.IsObserving;
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
            ? "F13 foi enviado com a marca do NinoBind. Se a observação estiver ligada, a tecla deve aparecer como eco ignorado."
            : $"O Windows aceitou {sent} de 2 eventos de teste.";
    }

    private void OnInput(ObservedInput input)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnInput(input));
            return;
        }

        if (input.Origin != InputOrigin.IgnoredEcho && input.DeviceHandle != 0)
            _lastSeen[input.DeviceHandle] = DateTime.UtcNow;

        var device = input.Origin == InputOrigin.IgnoredEcho
            ? "eco do NinoBind"
            : DeviceLabel(input.DeviceHandle);
        WaitingRow.Visibility = Visibility.Collapsed;
        EventList.Items.Insert(0, new LiveEvent(device, input.Description.ToLowerInvariant(), DateTime.Now.ToString("HH:mm:ss")));
        while (EventList.Items.Count > EventLimit)
            EventList.Items.RemoveAt(EventList.Items.Count - 1);
        if (_editingKey is null)
            ShowDevices();
    }

    private string DeviceLabel(nint handle)
    {
        if (handle == 0)
            return "Aparelho não separado";
        var device = _session.Devices.FirstOrDefault(item => item.Handle == handle);
        return device is null
            ? "Aparelho não separado"
            : DeviceLabels.Friendly(device, _session.Devices, _host.Configuration.Document.Settings.DeviceNicknames);
    }

    private sealed record LiveEvent(string Device, string Detail, string Time);
}
