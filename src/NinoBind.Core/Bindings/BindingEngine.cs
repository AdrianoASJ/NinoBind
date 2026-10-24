using NinoBind.Core.Configuration;

namespace NinoBind.Core.Bindings;

public static class BindingKinds
{
    public const string Key = "key";
    public const string Mouse = "mouse";
    public const string None = "none";
    public const string Chord = "chord";
    public const string Macro = "macro";
    public const string Text = "text";
    public const string Suppress = "suppress";
}

public enum BindingMatch
{
    None,
    Conflict,
    Ready,
}

public sealed record BindingMatchResult(BindingMatch Match, BindingDocument? Binding, IReadOnlyList<string> ConflictIds);

public readonly record struct PlaybackStep(string Kind, int Code, bool Down, string? Text);

public static class BindingEngine
{
    public const int ReservedKey = 0x13;
    public const int ReservedScrollLockKey = 0x91;

    public static bool IsReservedKey(int code) =>
        code is ReservedKey or ReservedScrollLockKey;

    public static bool AppliesOnGlobalHook(BindingDocument binding) =>
        string.IsNullOrEmpty(binding.DeviceId);

    public static bool ScopesOverlap(string left, string right) =>
        string.IsNullOrEmpty(left)
        || string.IsNullOrEmpty(right)
        || string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public static BindingMatchResult Resolve(ConfigurationDocument document, string inputKind, int inputCode)
    {
        if (inputKind == BindingKinds.Key && IsReservedKey(inputCode))
            return new BindingMatchResult(BindingMatch.None, null, []);
        var matches = document.Bindings
            .Where(binding => binding.IsEnabled && binding.ProfileId == document.ActiveProfileId)
            .Where(binding => binding.InputKind == inputKind && binding.InputCode == inputCode)
            .ToList();
        var hookMatches = matches.Where(AppliesOnGlobalHook).ToList();
        var blocked = hookMatches.Any(hook => matches.Any(other =>
            other.Id != hook.Id && ScopesOverlap(hook.DeviceId, other.DeviceId)));
        if (blocked || hookMatches.Count > 1)
            return new BindingMatchResult(BindingMatch.Conflict, null, matches.Select(binding => binding.Id).ToList());
        if (hookMatches.Count == 0)
            return new BindingMatchResult(BindingMatch.None, null, []);
        var selected = hookMatches[0];
        if (selected.ActionKind == BindingKinds.None || IsEmptyMacro(document, selected))
            return new BindingMatchResult(BindingMatch.None, selected, []);
        return new BindingMatchResult(BindingMatch.Ready, selected, []);
    }

    public static MacroDocument? FindMacro(ConfigurationDocument document, BindingDocument binding) =>
        binding.ActionKind == BindingKinds.Macro
            ? document.Macros.FirstOrDefault(macro => macro.Id == binding.MacroId)
            : null;

    private static bool IsEmptyMacro(ConfigurationDocument document, BindingDocument binding) =>
        binding.ActionKind == BindingKinds.Macro && (FindMacro(document, binding)?.Steps.Count ?? 0) == 0;

    public static IReadOnlyList<PlaybackStep> Press(BindingDocument binding) => binding.ActionKind switch
    {
        BindingKinds.Key => [Step("key", binding.OutputCode, true)],
        BindingKinds.Mouse => [Step("mouse", binding.OutputCode, true)],
        BindingKinds.Chord => binding.Chord.Select(key => Step("key", key, true)).ToList(),
        BindingKinds.Text => [new PlaybackStep("text", 0, true, binding.Text)],
        _ => [],
    };

    public static IReadOnlyList<PlaybackStep> Release(BindingDocument binding) => binding.ActionKind switch
    {
        BindingKinds.Key => [Step("key", binding.OutputCode, false)],
        BindingKinds.Mouse => [Step("mouse", binding.OutputCode, false)],
        BindingKinds.Chord => binding.Chord.AsEnumerable().Reverse().Select(key => Step("key", key, false)).ToList(),
        _ => [],
    };

    public static string Status(ConfigurationDocument document, BindingDocument binding)
    {
        if (!binding.IsEnabled)
            return "Inativo";
        var sameInput = document.Bindings.Where(other =>
            other.IsEnabled
            && other.ProfileId == binding.ProfileId
            && other.InputKind == binding.InputKind
            && other.InputCode == binding.InputCode
            && ScopesOverlap(other.DeviceId, binding.DeviceId)).ToList();
        if (sameInput.Count > 1)
            return "Conflito";
        if (!AppliesOnGlobalHook(binding))
            return "Não aplicado";
        if (IsEmptyMacro(document, binding))
            return "Macro vazia";
        if (binding.ActionKind == BindingKinds.None)
            return "Sem ação";
        return "Ativo";
    }

    private static PlaybackStep Step(string kind, int code, bool down) => new(kind, code, down, null);
}

public sealed class HeldOutputs
{
    private readonly Dictionary<string, IReadOnlyList<PlaybackStep>> _releases = [];

    public void Hold(string source, IReadOnlyList<PlaybackStep> releases) => _releases[source] = releases;

    public bool TryTake(string source, out IReadOnlyList<PlaybackStep> steps)
    {
        var found = _releases.Remove(source, out var stored);
        steps = stored ?? [];
        return found;
    }

    public IReadOnlyList<PlaybackStep> Take(string source) =>
        TryTake(source, out var steps) ? steps : [];

    public IReadOnlyList<PlaybackStep> TakeAll()
    {
        var steps = _releases.Values.SelectMany(item => item).ToList();
        _releases.Clear();
        return steps;
    }
}
