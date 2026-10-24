using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;
using NinoBind.Core.Macros;
using NinoBind.Input;

namespace NinoBind.Hosting;

public sealed class BindingController : IMacroOutput
{
    private const int MaxConcurrentMacros = 8;
    private readonly DesktopInputSession _session;
    private readonly HeldOutputs _held = new();
    private readonly Dictionary<string, CancellationTokenSource> _runs = [];
    private readonly object _gate = new();
    private readonly HashSet<string> _macroSources = [];
    private readonly HashSet<string> _whileHeld = [];
    private ConfigurationDocument _document;

    public BindingController(DesktopInputSession session, ConfigurationDocument document)
    {
        _session = session;
        _document = document;
        _session.Decide = Decide;
    }

    public event Action<int>? MacrosChanged;

    public int RunningMacros
    {
        get
        {
            lock (_gate)
                return _runs.Count;
        }
    }

    public void Use(ConfigurationDocument document) => _document = document;

    public void ReleaseHeld()
    {
        CancelAllMacros();
        _macroSources.Clear();
        _whileHeld.Clear();
        foreach (var step in _held.TakeAll())
            Send(step);
    }

    public int EmergencyStop()
    {
        var running = RunningMacros;
        ReleaseHeld();
        return running;
    }

    public string? Test(BindingDocument binding)
    {
        if (binding.ActionKind is BindingKinds.None or BindingKinds.Suppress)
            return "Essa ação não envia uma entrada.";
        if (binding.ActionKind == BindingKinds.Macro)
        {
            var macro = BindingEngine.FindMacro(_document, binding);
            if (macro is null || macro.Steps.Count == 0)
                return "A macro está vazia.";
            return StartMacro("test:" + binding.Id, macro) ? null : "Há macros demais em execução.";
        }

        foreach (var step in BindingEngine.Press(binding))
            Send(step);
        foreach (var step in BindingEngine.Release(binding))
            Send(step);
        return null;
    }

    public bool StartMacro(string source, MacroDocument macro)
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_runs.ContainsKey(source) || _runs.Count >= MaxConcurrentMacros)
                return false;
            cancellation = new CancellationTokenSource();
            _runs[source] = cancellation;
        }

        MacrosChanged?.Invoke(RunningMacros);
        _ = Task.Run(async () =>
        {
            try
            {
                await MacroRunner.RunAsync(macro, this, Task.Delay, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    if (_runs.TryGetValue(source, out var current) && current == cancellation)
                        _ = _runs.Remove(source);
                }

                cancellation.Dispose();
                MacrosChanged?.Invoke(RunningMacros);
            }
        });
        return true;
    }

    public void CancelMacro(string source)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(source, out var cancellation))
                cancellation.Cancel();
        }
    }

    public void Send(PlaybackStep step)
    {
        switch (step.Kind)
        {
            case "key":
                _session.SendKey((ushort)step.Code, step.Down);
                break;
            case "mouse":
                _session.SendMouseButton(step.Code, step.Down);
                break;
            case "text" when !string.IsNullOrEmpty(step.Text):
                _session.SendUnicode(step.Text);
                break;
        }
    }

    private void CancelAllMacros()
    {
        lock (_gate)
        {
            foreach (var cancellation in _runs.Values)
                cancellation.Cancel();
        }
    }

    private bool Decide(PhysicalEdge edge)
    {
        var source = Source(edge);
        if (!edge.IsDown)
        {
            if (!_held.TryTake(source, out var ups))
                return false;
            CancelWhileHeld(source);
            foreach (var step in ups)
                Send(step);
            return true;
        }

        if (_macroSources.Contains(source))
            return true;

        var kind = edge.IsKeyboard ? BindingKinds.Key : BindingKinds.Mouse;
        var result = BindingEngine.Resolve(_document, kind, edge.Code);
        if (result.Match != BindingMatch.Ready || result.Binding is null)
            return false;

        if (result.Binding.ActionKind == BindingKinds.Macro)
        {
            var macro = BindingEngine.FindMacro(_document, result.Binding);
            if (macro is null)
                return false;
            _held.Hold(source, []);
            _macroSources.Add(source);
            if (macro.RepeatMode == MacroKinds.RepeatWhileHeld)
                _whileHeld.Add(source);
            _ = StartMacro(source, macro);
            return true;
        }

        _held.Hold(source, BindingEngine.Release(result.Binding));
        foreach (var step in BindingEngine.Press(result.Binding))
            Send(step);
        return true;
    }

    private void CancelWhileHeld(string source)
    {
        _macroSources.Remove(source);
        if (_whileHeld.Remove(source))
            CancelMacro(source);
    }

    private static string Source(PhysicalEdge edge) =>
        (edge.IsKeyboard ? BindingKinds.Key : BindingKinds.Mouse) + ":" + edge.Code;
}
