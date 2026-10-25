using System.Windows;
using System.Windows.Controls;
using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;
using NinoBind.Input;

namespace NinoBind.Views;

public partial class BindingEditorWindow : Window
{
    private readonly DesktopInputSession _session;
    private readonly nint _ownerWindow;
    private readonly List<InputDevice> _devices;
    private bool _capturingOutput;
    private bool _capturing;
    private string _inputKind = BindingKinds.Key;
    private int _inputCode;
    private int _outputCode;

    public BindingDocument? Result { get; private set; }

    public BindingEditorWindow(
        ConfigurationDocument document,
        DesktopInputSession session,
        nint ownerWindow,
        IReadOnlyList<InputDevice> devices,
        BindingDocument? existing)
    {
        _session = session;
        _ownerWindow = ownerWindow;
        _devices = devices.ToList();
        InitializeComponent();
        OverlayWindow.Attach(this);
        TitleText.Text = existing is null ? "Novo binding" : "Editar binding";
        DeviceBox.Items.Add("Qualquer dispositivo");
        foreach (var device in _devices)
            DeviceBox.Items.Add(DeviceLabels.Friendly(device, _devices, document.Settings.DeviceNicknames));
        DeviceBox.SelectedIndex = 0;
        foreach (var profile in document.Profiles)
            ProfileBox.Items.Add(profile);
        ProfileBox.SelectedItem = document.Profiles.FirstOrDefault(profile => profile.Id == (existing?.ProfileId ?? document.ActiveProfileId));
        ActionBox.ItemsSource = new[]
        {
            "Nenhuma ação",
            "Tecla",
            "Atalho",
            "Botão do mouse",
            "Macro",
            "Texto",
            "Desativar entrada",
        };
        for (var button = 1; button <= 5; button++)
            MouseBox.Items.Add(InputNames.MouseButton(button));
        MouseBox.SelectedIndex = 0;
        foreach (var macro in document.Macros)
            MacroBox.Items.Add(new MacroOption(macro));
        if (existing is not null)
            Load(existing);
        else
            ActionBox.SelectedIndex = 1;
        Closed += (_, _) =>
        {
            _session.Captured -= OnCaptured;
            _session.EndCapture();
        };
    }

    private void Load(BindingDocument binding)
    {
        _inputKind = binding.InputKind;
        _inputCode = binding.InputCode;
        _outputCode = binding.OutputCode;
        ShowTrigger();
        OutputText.Text = _outputCode == 0 ? "Não escolhida" : InputNames.Key((ushort)_outputCode);
        if (!string.IsNullOrEmpty(binding.DeviceId))
        {
            var index = _devices.FindIndex(device => device.DevicePath == binding.DeviceId);
            DeviceBox.SelectedIndex = index >= 0 ? index + 1 : 0;
        }

        ActionBox.SelectedIndex = binding.ActionKind switch
        {
            BindingKinds.Key => 1,
            BindingKinds.Chord => 2,
            BindingKinds.Mouse => 3,
            BindingKinds.Macro => 4,
            BindingKinds.Text => 5,
            BindingKinds.Suppress => 6,
            _ => 0,
        };
        CtrlBox.IsChecked = binding.Chord.Contains(0xA2);
        ShiftBox.IsChecked = binding.Chord.Contains(0xA0);
        AltBox.IsChecked = binding.Chord.Contains(0xA4);
        WinBox.IsChecked = binding.Chord.Contains(0x5B);
        if (binding.ActionKind == BindingKinds.Mouse && binding.OutputCode is >= 1 and <= 5)
            MouseBox.SelectedIndex = binding.OutputCode - 1;
        TextBox.Text = binding.Text ?? "";
        MacroBox.SelectedItem = MacroBox.Items.OfType<MacroOption>().FirstOrDefault(item => item.Macro.Id == binding.MacroId);
        Tag = binding.Id;
    }

    private void CaptureTriggerClicked(object sender, RoutedEventArgs e) => Arm(output: false);

    private void CaptureOutputClicked(object sender, RoutedEventArgs e) => Arm(output: true);

    private void Arm(bool output)
    {
        _capturingOutput = output;
        _capturing = true;
        HintText.Text = output ? "Pressione a tecla de saída. Esc cancela a captura." : "Esc cancela a captura.";
        if (!output)
        {
            TriggerText.Text = "Pressione uma tecla ou botão…";
            TriggerText.Foreground = (System.Windows.Media.Brush)FindResource("GoldBrush");
            CurrentText.Text = _inputCode == 0 ? "" : "atual: " + DescribeInput();
            CaptureField.BorderBrush = (System.Windows.Media.Brush)FindResource("GoldBrush");
        }
        _session.Captured -= OnCaptured;
        _session.Captured += OnCaptured;
        if (!_session.BeginCapture(_ownerWindow))
        {
            _capturing = false;
            HintText.Text = _session.ObservationError ?? "A captura não foi armada.";
        }
    }

    private void StopCapture()
    {
        _capturing = false;
        _session.Captured -= OnCaptured;
        _session.EndCapture();
        CaptureField.BorderBrush = (System.Windows.Media.Brush)FindResource("FieldLineBrush");
        ShowTrigger();
    }

    private void ShowTrigger()
    {
        if (_inputCode == 0)
        {
            TriggerText.Text = "Pressione uma tecla ou botão…";
            TriggerText.Foreground = (System.Windows.Media.Brush)FindResource("TextDimBrush");
            CurrentText.Text = "";
            return;
        }

        TriggerText.Text = DescribeInput();
        TriggerText.Foreground = (System.Windows.Media.Brush)FindResource("TextBrush");
        CurrentText.Text = "";
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || !_capturing)
            return;
        StopCapture();
        HintText.Text = "Captura cancelada.";
        e.Handled = true;
    }

    private void OnCaptured(PhysicalEdge edge)
    {
        Dispatcher.Invoke(() =>
        {
            var wasOutput = _capturingOutput;
            if (wasOutput)
            {
                if (!edge.IsKeyboard)
                {
                    StopCapture();
                    HintText.Text = "A saída de tecla aceita apenas teclado.";
                    return;
                }

                _outputCode = edge.Code;
                OutputText.Text = InputNames.Key((ushort)edge.Code);
                StopCapture();
            }
            else
            {
                _inputKind = edge.IsKeyboard ? BindingKinds.Key : BindingKinds.Mouse;
                _inputCode = edge.Code;
                if (edge.IsKeyboard && BindingEngine.IsReservedKey(edge.Code))
                {
                    _inputCode = 0;
                    StopCapture();
                    HintText.Text = edge.Code == BindingEngine.ReservedScrollLockKey
                        ? "Scroll Lock fica reservada para ligar e desligar o motor."
                        : "Pause fica reservada para o Emergency Stop.";
                    return;
                }

                StopCapture();
                if (!edge.IsKeyboard && edge.Code == 1)
                {
                    HintText.Text = "Atenção: com o motor ligado, o botão esquerdo deixa de clicar normalmente em todos os aplicativos. Prefira um botão lateral (Botão 4 ou 5).";
                    return;
                }
            }

            HintText.Text = "Entrada capturada. Ela não foi enviada ao aplicativo em foco.";
        });
    }

    private void ActionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeyPanel is null)
            return;
        var action = SelectedAction();
        KeyPanel.Visibility = action is BindingKinds.Key or BindingKinds.Chord ? Visibility.Visible : Visibility.Collapsed;
        ChordPanel.Visibility = action == BindingKinds.Chord ? Visibility.Visible : Visibility.Collapsed;
        MousePanel.Visibility = action == BindingKinds.Mouse ? Visibility.Visible : Visibility.Collapsed;
        TextPanel.Visibility = action == BindingKinds.Text ? Visibility.Visible : Visibility.Collapsed;
        MacroPanel.Visibility = action == BindingKinds.Macro ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (_inputCode == 0)
        {
            HintText.Text = "Capture a entrada antes de salvar.";
            return;
        }

        var profile = ProfileBox.SelectedItem as ProfileDocument;
        if (profile is null)
        {
            HintText.Text = "Escolha um perfil.";
            return;
        }

        var action = SelectedAction();
        var binding = new BindingDocument
        {
            Id = Tag as string ?? Guid.NewGuid().ToString("N"),
            ProfileId = profile.Id,
            InputKind = _inputKind,
            InputCode = _inputCode,
            ActionKind = action,
            IsEnabled = true,
        };
        if (DeviceBox.SelectedIndex > 0)
        {
            var device = _devices[DeviceBox.SelectedIndex - 1];
            if (!string.IsNullOrEmpty(device.DevicePath))
            {
                binding.DeviceId = device.DevicePath;
                binding.DeviceLabel = device.DisplayName;
            }
        }

        switch (action)
        {
            case BindingKinds.Key:
                binding.OutputCode = _outputCode;
                break;
            case BindingKinds.Chord:
                binding.Chord = ChordKeys();
                break;
            case BindingKinds.Mouse:
                binding.OutputCode = MouseBox.SelectedIndex + 1;
                break;
            case BindingKinds.Text:
                binding.Text = TextBox.Text;
                break;
            case BindingKinds.Macro:
                binding.MacroId = (MacroBox.SelectedItem as MacroOption)?.Macro.Id ?? "";
                break;
        }

        Result = binding;
        DialogResult = true;
    }

    private void CancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;

    private string SelectedAction() => ActionBox.SelectedIndex switch
    {
        1 => BindingKinds.Key,
        2 => BindingKinds.Chord,
        3 => BindingKinds.Mouse,
        4 => BindingKinds.Macro,
        5 => BindingKinds.Text,
        6 => BindingKinds.Suppress,
        _ => BindingKinds.None,
    };

    private List<int> ChordKeys()
    {
        var keys = new List<int>();
        if (CtrlBox.IsChecked == true)
            keys.Add(0xA2);
        if (ShiftBox.IsChecked == true)
            keys.Add(0xA0);
        if (AltBox.IsChecked == true)
            keys.Add(0xA4);
        if (WinBox.IsChecked == true)
            keys.Add(0x5B);
        if (_outputCode > 0)
            keys.Add(_outputCode);
        return keys;
    }

    private string DescribeInput() => _inputKind == BindingKinds.Mouse
        ? InputNames.MouseButton(_inputCode)
        : InputNames.Key((ushort)_inputCode);

    private sealed class MacroOption(MacroDocument macro)
    {
        public MacroDocument Macro { get; } = macro;

        public override string ToString() =>
            $"{Macro.Name} · {Macro.Steps.Count} {(Macro.Steps.Count == 1 ? "passo" : "passos")}";
    }
}
