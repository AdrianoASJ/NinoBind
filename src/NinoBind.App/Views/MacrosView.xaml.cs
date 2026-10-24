using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NinoBind.Core.Configuration;
using NinoBind.Core.Macros;
using NinoBind.Hosting;
using NinoBind.Input;

namespace NinoBind.Views;

public partial class MacrosView : UserControl
{
    private readonly AppHost _host;
    private readonly BindingController _controller;
    private readonly Action _saved;

    public MacrosView(AppHost host, BindingController controller, Action saved)
    {
        _host = host;
        _controller = controller;
        _saved = saved;
        InitializeComponent();
        IntroText.Text = $"Uma macro roda quando um binding de macro dispara ou pelo botão Testar. {EmergencyHotkey.Label} para todas as macros e solta as teclas que elas seguravam, mesmo com outra janela em foco.";
        Loaded += (_, _) => _controller.MacrosChanged += OnMacrosChanged;
        Unloaded += (_, _) => _controller.MacrosChanged -= OnMacrosChanged;
        Reload();
    }

    public void Reload()
    {
        var document = _host.Configuration.Document;
        MacroList.Items.Clear();
        foreach (var macro in document.Macros)
        {
            var repeat = macro.RepeatMode == MacroKinds.RepeatWhileHeld
                ? "Enquanto segurar"
                : macro.RepeatCount == 1 ? "Uma vez" : $"{macro.RepeatCount} vezes";
            var delay = macro.Steps.Where(step => step.Kind == MacroKinds.Delay).Sum(step => (long)step.DelayMs);
            var uses = document.Bindings.Count(binding => binding.MacroId == macro.Id);
            MacroList.Items.Add(new MacroRow(macro.Id, macro.Name, macro.Steps.Count, repeat, $"{delay} ms", uses));
        }

        ShowRunning(_controller.RunningMacros);
    }

    private void AddClicked(object sender, RoutedEventArgs e) => Edit(null);

    private void EditClicked(object sender, RoutedEventArgs e)
    {
        if (Selected() is { } macro)
            Edit(macro);
    }

    private void DuplicateClicked(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } source)
            return;
        Commit(new MacroDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = source.Name + " (cópia)",
            RepeatMode = source.RepeatMode,
            RepeatCount = source.RepeatCount,
            Steps = source.Steps.Select(MacroEditorWindow.Copy).ToList(),
        });
    }

    private void DeleteClicked(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } macro)
            return;
        var document = _host.Configuration.Document;
        var uses = document.Bindings.Count(binding => binding.MacroId == macro.Id);
        if (uses > 0)
        {
            StatusText.Text = $"Essa macro está em {uses} binding(s). Troque a ação deles antes de excluir.";
            return;
        }

        if (MessageBox.Show($"Excluir a macro {macro.Name}?", "NinoBind", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var index = document.Macros.IndexOf(macro);
        document.Macros.RemoveAt(index);
        if (!_host.TrySave(out var error))
        {
            document.Macros.Insert(index, macro);
            StatusText.Text = error ?? "A alteração não foi salva.";
            return;
        }

        _saved();
        Reload();
    }

    private void TestClicked(object sender, RoutedEventArgs e)
    {
        if (Selected() is not { } macro)
            return;
        if (macro.Steps.Count == 0)
        {
            StatusText.Text = "A macro está vazia.";
            return;
        }

        var testMacro = macro.RepeatMode == MacroKinds.RepeatWhileHeld
            ? new MacroDocument { Id = macro.Id, Name = macro.Name, RepeatMode = MacroKinds.RepeatCount, RepeatCount = 1, Steps = macro.Steps }
            : macro;
        StatusText.Text = "A macro começa em 2 segundos. Clique na janela que deve recebê-la.";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_controller.StartMacro("test:" + macro.Id, testMacro))
                StatusText.Text = "Essa macro já está em execução.";
        };
        timer.Start();
    }

    private void StopClicked(object sender, RoutedEventArgs e)
    {
        var stopped = _controller.EmergencyStop();
        StatusText.Text = stopped == 0 ? "Nenhuma macro estava em execução." : $"{stopped} macro(s) parada(s).";
    }

    private void Edit(MacroDocument? macro)
    {
        var editor = new MacroEditorWindow(macro) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() == true && editor.Result is not null)
            Commit(editor.Result);
    }

    private void Commit(MacroDocument macro)
    {
        var list = _host.Configuration.Document.Macros;
        var index = list.FindIndex(item => item.Id == macro.Id);
        MacroDocument? previous = null;
        if (index >= 0)
        {
            previous = list[index];
            list[index] = macro;
        }
        else
        {
            list.Add(macro);
        }

        var error = ConfigurationValidator.Validate(_host.Configuration.Document);
        if (error is not null || !_host.TrySave(out error))
        {
            if (previous is null)
                list.RemoveAt(list.Count - 1);
            else
                list[index] = previous;
            StatusText.Text = error ?? "A alteração não foi salva.";
            return;
        }

        _controller.Use(_host.Configuration.Document);
        _saved();
        Reload();
    }

    private void OnMacrosChanged(int running) => Dispatcher.BeginInvoke(() => ShowRunning(running));

    private void ShowRunning(int running)
    {
        StatusText.Text = running switch
        {
            0 => _host.Configuration.Document.Macros.Count == 0 ? "Nenhuma macro salva." : "Nenhuma macro em execução.",
            1 => "1 macro em execução.",
            _ => $"{running} macros em execução.",
        };
    }

    private MacroDocument? Selected() =>
        MacroList.SelectedItem is MacroRow row
            ? _host.Configuration.Document.Macros.FirstOrDefault(item => item.Id == row.Id)
            : null;

    private sealed record MacroRow(string Id, string Name, int Steps, string Repeat, string Delay, int Uses);
}
