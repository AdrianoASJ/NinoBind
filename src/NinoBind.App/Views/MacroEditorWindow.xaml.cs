using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NinoBind.Core.Configuration;
using NinoBind.Core.Macros;
using NinoBind.Input;

namespace NinoBind.Views;

public partial class MacroEditorWindow : Window
{
    private const int MinRecordedDelayMs = 10;
    private readonly List<MacroStepDocument> _steps = [];
    private readonly Stopwatch _clock = new();
    private readonly string _id;
    private bool _recording;
    private bool _capturingKey;

    public MacroDocument? Result { get; private set; }

    public MacroEditorWindow(MacroDocument? existing)
    {
        InitializeComponent();
        RepeatBox.ItemsSource = new[] { "Repetir um número de vezes", "Repetir enquanto segurar" };
        for (var button = 1; button <= 5; button++)
            MouseBox.Items.Add(InputNames.MouseButton(button));
        MouseBox.SelectedIndex = 0;
        _id = existing?.Id ?? Guid.NewGuid().ToString("N");
        NameBox.Text = existing?.Name ?? "Nova macro";
        RepeatBox.SelectedIndex = existing?.RepeatMode == MacroKinds.RepeatWhileHeld ? 1 : 0;
        CountBox.Text = (existing?.RepeatCount ?? 1).ToString();
        if (existing is not null)
            _steps.AddRange(existing.Steps.Select(Copy));
        Refresh();
    }

    private void RecordClicked(object sender, RoutedEventArgs e)
    {
        _recording = !_recording;
        _capturingKey = false;
        RecordBanner.Visibility = _recording ? Visibility.Visible : Visibility.Collapsed;
        RecordButton.Content = _recording ? "Parar gravação" : "Gravar";
        if (_recording)
            _clock.Restart();
        else
            _clock.Reset();
        HintText.Text = _recording ? "" : "Gravação encerrada.";
        Focus();
    }

    private void RepeatChanged(object sender, SelectionChangedEventArgs e) =>
        CountBox.IsEnabled = RepeatBox.SelectedIndex != 1;

    private void AddKeyClicked(object sender, RoutedEventArgs e)
    {
        if (_recording)
            return;
        _capturingKey = true;
        HintText.Text = "Pressione a tecla com esta janela em foco.";
        Focus();
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording && !_capturingKey)
            return;
        var code = VirtualKey(e);
        e.Handled = true;
        if (code is < 1 or > 254 || e.IsRepeat)
            return;
        if (_capturingKey)
        {
            _capturingKey = false;
            Insert(Step(MacroKinds.Key, code, true));
            Insert(Step(MacroKinds.Key, code, false));
            HintText.Text = $"{InputNames.Key((ushort)code)} adicionada.";
            return;
        }

        Record(Step(MacroKinds.Key, code, true));
    }

    private void WindowKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recording)
            return;
        var code = VirtualKey(e);
        e.Handled = true;
        if (code is >= 1 and <= 254)
            Record(Step(MacroKinds.Key, code, false));
    }

    private void PadMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_recording)
            return;
        e.Handled = true;
        Record(Step(MacroKinds.Mouse, MouseCode(e.ChangedButton), true));
    }

    private void PadMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_recording)
            return;
        e.Handled = true;
        Record(Step(MacroKinds.Mouse, MouseCode(e.ChangedButton), false));
    }

    private void Record(MacroStepDocument step)
    {
        if (_steps.Count + 2 > MacroLimits.MaxSteps)
        {
            RecordClicked(this, new RoutedEventArgs());
            HintText.Text = $"A gravação parou no limite de {MacroLimits.MaxSteps} passos.";
            return;
        }

        var elapsed = (int)Math.Min(_clock.ElapsedMilliseconds, MacroLimits.MaxDelayMs);
        _clock.Restart();
        if (_steps.Count > 0 && elapsed >= MinRecordedDelayMs)
            _steps.Add(new MacroStepDocument { Kind = MacroKinds.Delay, DelayMs = elapsed });
        _steps.Add(step);
        Refresh(_steps.Count - 1);
    }

    private void AddClickClicked(object sender, RoutedEventArgs e)
    {
        var button = MouseBox.SelectedIndex + 1;
        Insert(Step(MacroKinds.Mouse, button, true));
        Insert(Step(MacroKinds.Mouse, button, false));
    }

    private void AddDelayClicked(object sender, RoutedEventArgs e)
    {
        if (ReadDelay() is { } delay)
            Insert(new MacroStepDocument { Kind = MacroKinds.Delay, DelayMs = delay });
    }

    private void ChangeDelayClicked(object sender, RoutedEventArgs e)
    {
        var index = StepList.SelectedIndex;
        if (index < 0 || _steps[index].Kind != MacroKinds.Delay)
        {
            HintText.Text = "Selecione um passo de atraso.";
            return;
        }

        if (ReadDelay() is { } delay)
        {
            _steps[index].DelayMs = delay;
            Refresh(index);
        }
    }

    private void AddTextClicked(object sender, RoutedEventArgs e)
    {
        var text = StepTextBox.Text;
        if (string.IsNullOrEmpty(text) || text.Any(char.IsControl))
        {
            HintText.Text = "Digite um texto sem quebras de linha.";
            return;
        }

        Insert(new MacroStepDocument { Kind = MacroKinds.Text, Text = text });
        StepTextBox.Clear();
    }

    private void UpClicked(object sender, RoutedEventArgs e) => Move(-1);

    private void DownClicked(object sender, RoutedEventArgs e) => Move(1);

    private void DuplicateStepClicked(object sender, RoutedEventArgs e)
    {
        var index = StepList.SelectedIndex;
        if (index >= 0)
            Insert(Copy(_steps[index]));
    }

    private void RemoveClicked(object sender, RoutedEventArgs e)
    {
        var index = StepList.SelectedIndex;
        if (index < 0)
            return;
        _steps.RemoveAt(index);
        Refresh(Math.Min(index, _steps.Count - 1));
    }

    private void StepListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || _recording || _capturingKey)
            return;
        e.Handled = true;
        RemoveClicked(sender, e);
    }

    private void StepSelected(object sender, SelectionChangedEventArgs e)
    {
        UpdateStepButtons();
        var index = StepList.SelectedIndex;
        if (index >= 0 && _steps[index].Kind == MacroKinds.Delay)
            DelayBox.Text = _steps[index].DelayMs.ToString();
    }

    private void UpdateStepButtons()
    {
        var index = StepList.SelectedIndex;
        UpButton.IsEnabled = index > 0;
        DownButton.IsEnabled = index >= 0 && index < _steps.Count - 1;
        DuplicateButton.IsEnabled = index >= 0;
        RemoveButton.IsEnabled = index >= 0;
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (_recording)
            RecordClicked(this, new RoutedEventArgs());
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            HintText.Text = "Dê um nome à macro.";
            return;
        }

        if (!int.TryParse(CountBox.Text, out var count))
            count = 0;
        var macro = new MacroDocument
        {
            Id = _id,
            Name = name,
            RepeatMode = RepeatBox.SelectedIndex == 1 ? MacroKinds.RepeatWhileHeld : MacroKinds.RepeatCount,
            RepeatCount = count,
            Steps = _steps.Select(Copy).ToList(),
        };
        var error = MacroRunner.Validate(macro);
        if (error is null && macro.RepeatMode == MacroKinds.RepeatWhileHeld && !macro.Steps.Any(step => step.Kind == MacroKinds.Delay && step.DelayMs > 0))
            error = "Para repetir enquanto segurar, adicione um atraso (por exemplo, 50 ms). Sem atraso, a macro roda todas as repetições de uma vez e para.";
        if (error is not null)
        {
            HintText.Text = error;
            return;
        }

        Result = macro;
        DialogResult = true;
    }

    private void CancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Move(int offset)
    {
        var index = StepList.SelectedIndex;
        var target = index + offset;
        if (index < 0 || target < 0 || target >= _steps.Count)
            return;
        (_steps[index], _steps[target]) = (_steps[target], _steps[index]);
        Refresh(target);
    }

    private void Insert(MacroStepDocument step)
    {
        if (_steps.Count >= MacroLimits.MaxSteps)
        {
            HintText.Text = $"A macro já tem {MacroLimits.MaxSteps} passos.";
            return;
        }

        var index = StepList.SelectedIndex < 0 ? _steps.Count : StepList.SelectedIndex + 1;
        _steps.Insert(index, step);
        Refresh(index);
    }

    private int? ReadDelay()
    {
        if (int.TryParse(DelayBox.Text, out var delay) && delay is >= 0 and <= MacroLimits.MaxDelayMs)
            return delay;
        HintText.Text = $"O atraso precisa ficar entre 0 e {MacroLimits.MaxDelayMs} ms.";
        return null;
    }

    private void Refresh(int select = -1)
    {
        StepList.Items.Clear();
        foreach (var step in _steps)
            StepList.Items.Add(Describe(step));
        StepList.SelectedIndex = select;
        if (select >= 0)
            StepList.ScrollIntoView(StepList.Items[select]);
        UpdateStepButtons();
    }

    public static string Describe(MacroStepDocument step) => step.Kind switch
    {
        MacroKinds.Key => (step.Down ? "Pressionar " : "Soltar ") + InputNames.Key((ushort)step.Code),
        MacroKinds.Mouse => (step.Down ? "Pressionar " : "Soltar ") + InputNames.MouseButton(step.Code),
        MacroKinds.Delay => $"Esperar {step.DelayMs} ms",
        MacroKinds.Text => $"Digitar texto ({step.Text.Length} caracteres)",
        _ => "Passo desconhecido",
    };

    private static int VirtualKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return KeyInterop.VirtualKeyFromKey(key);
    }

    private static int MouseCode(MouseButton button) => button switch
    {
        MouseButton.Left => 1,
        MouseButton.Right => 2,
        MouseButton.Middle => 3,
        MouseButton.XButton1 => 4,
        _ => 5,
    };

    private static MacroStepDocument Step(string kind, int code, bool down) =>
        new() { Kind = kind, Code = code, Down = down };

    public static MacroStepDocument Copy(MacroStepDocument step) => new()
    {
        Kind = step.Kind,
        Code = step.Code,
        Down = step.Down,
        DelayMs = step.DelayMs,
        Text = step.Text,
    };
}
