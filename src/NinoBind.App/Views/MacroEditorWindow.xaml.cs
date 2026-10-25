using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly List<InputChoice> _choices = [];
    private bool _recording;
    private bool _capturingKey;
    private Point _dragStart;
    private bool _dragging;

    public MacroDocument? Result { get; private set; }

    public MacroEditorWindow(MacroDocument? existing)
    {
        InitializeComponent();
        OverlayWindow.Attach(this);
        RepeatBox.ItemsSource = new[] { "Número de vezes", "Enquanto segurar" };
        for (var button = 1; button <= 5; button++)
            _choices.Add(new InputChoice(true, button, InputNames.MouseButton(button)));
        _choices.Add(new InputChoice(false, 0, "Capturar tecla…"));
        MouseBox.ItemsSource = _choices;
        MouseBox.DisplayMemberPath = "Name";
        MouseBox.SelectedIndex = 0;
        MouseBox.SelectionChanged += ChoiceChanged;
        _id = existing?.Id ?? Guid.NewGuid().ToString("N");
        NameBox.Text = existing?.Name ?? "Nova macro";
        RepeatBox.SelectedIndex = existing?.RepeatMode == MacroKinds.RepeatWhileHeld ? 1 : 0;
        CountBox.Text = (existing?.RepeatCount ?? 1).ToString();
        if (existing is not null)
            _steps.AddRange(existing.Steps.Select(Copy));
        Refresh();
        CountPanel.Visibility = RepeatBox.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MouseBox.SelectedItem is not InputChoice choice || choice.IsMouse || choice.Code != 0)
            return;
        _capturingKey = true;
        HintText.Text = "Pressione a tecla com esta janela em foco.";
        MouseBox.SelectedIndex = 0;
        Focus();
    }

    private void RecordClicked(object sender, RoutedEventArgs e)
    {
        _recording = !_recording;
        _capturingKey = false;
        RecordBanner.Visibility = _recording ? Visibility.Visible : Visibility.Collapsed;
        RecordLabel.Text = _recording ? "Parar gravação" : "Gravar sequência";
        if (_recording)
            _clock.Restart();
        else
            _clock.Reset();
        HintText.Text = _recording ? "" : "Gravação encerrada.";
        Focus();
    }

    private void RepeatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CountPanel is null)
            return;
        CountPanel.Visibility = RepeatBox.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
    }

    private InputChoice SelectedChoice() =>
        MouseBox.SelectedItem as InputChoice ?? _choices[0];

    private void AddPressClicked(object sender, RoutedEventArgs e) => AddChoice(down: true, click: false);

    private void AddReleaseClicked(object sender, RoutedEventArgs e) => AddChoice(down: false, click: false);

    private void AddClickClicked(object sender, RoutedEventArgs e) => AddChoice(down: true, click: true);

    private void AddChoice(bool down, bool click)
    {
        if (_recording)
            return;
        var choice = SelectedChoice();
        var kind = choice.IsMouse ? MacroKinds.Mouse : MacroKinds.Key;
        Insert(Step(kind, choice.Code, down));
        if (click)
            Insert(Step(kind, choice.Code, false));
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
            RememberKey(code);
            HintText.Text = $"{InputNames.Key((ushort)code)} selecionada.";
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

    private void RememberKey(int code)
    {
        var name = InputNames.Key((ushort)code);
        var existing = _choices.FindIndex(choice => !choice.IsMouse && choice.Code == code);
        if (existing < 0)
        {
            _choices.Add(new InputChoice(false, code, name));
            MouseBox.ItemsSource = null;
            MouseBox.ItemsSource = _choices;
            existing = _choices.Count - 1;
        }

        MouseBox.SelectedIndex = existing;
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

    private void AddDelayClicked(object sender, RoutedEventArgs e)
    {
        if (ReadDelay() is { } delay)
            Insert(new MacroStepDocument { Kind = MacroKinds.Delay, DelayMs = delay });
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

    private void Move(int offset)
    {
        var index = StepList.SelectedIndex;
        var target = index + offset;
        if (index < 0 || target < 0 || target >= _steps.Count)
            return;
        (_steps[index], _steps[target]) = (_steps[target], _steps[index]);
        Refresh(target);
    }

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

    private void StepMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not int index)
            return;
        StepList.SelectedIndex = index;
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Duplicar", () => DuplicateStepClicked(sender, e)));
        menu.Items.Add(MenuItem("Mover para cima", () => Move(-1), index > 0));
        menu.Items.Add(MenuItem("Mover para baixo", () => Move(1), index >= 0 && index < _steps.Count - 1));
        menu.Items.Add(MenuItem("Excluir", () => RemoveClicked(sender, e)));
        menu.PlacementTarget = element;
        menu.IsOpen = true;
    }

    private static MenuItem MenuItem(string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    private void StepMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragging = false;
    }

    private void StepMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _recording || _dragging)
            return;
        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 8)
            return;
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
            return;
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: StepRow row })
            return;
        _dragging = true;
        _ = DragDrop.DoDragDrop(StepList, row.Index, DragDropEffects.Move);
        _dragging = false;
    }

    private void StepDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(int)) is not int from)
            return;
        var target = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        var to = target?.DataContext is StepRow row ? row.Index : _steps.Count - 1;
        if (from < 0 || to < 0 || from == to)
            return;
        var step = _steps[from];
        _steps.RemoveAt(from);
        if (to > from)
            to--;
        _steps.Insert(to, step);
        Refresh(to);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
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
        var index = StepList.SelectedIndex;
        if (index >= 0 && _steps[index].Kind == MacroKinds.Delay)
            DelayBox.Text = _steps[index].DelayMs.ToString();
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
        var delay = _steps.Where(step => step.Kind == MacroKinds.Delay).Sum(step => (long)step.DelayMs);
        StepCountText.Text = _steps.Count.ToString();
        DelayTotalText.Text = $"Atraso total {delay} ms";
        for (var i = 0; i < _steps.Count; i++)
            StepList.Items.Add(new StepRow(i, i + 1, Glyph(_steps[i]), Describe(_steps[i])));
        StepList.SelectedIndex = select;
        if (select >= 0 && select < StepList.Items.Count)
            StepList.ScrollIntoView(StepList.Items[select]);
    }

    private static string Glyph(MacroStepDocument step) => step.Kind switch
    {
        MacroKinds.Mouse => "\uE962",
        MacroKinds.Delay => "\uE121",
        MacroKinds.Text => "\uE8D2",
        _ => "\uE92E",
    };

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

    private sealed record InputChoice(bool IsMouse, int Code, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record StepRow(int Index, int Number, string Glyph, string Text);
}
