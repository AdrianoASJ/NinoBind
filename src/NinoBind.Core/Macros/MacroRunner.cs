using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;

namespace NinoBind.Core.Macros;

public static class MacroKinds
{
    public const string Key = "key";
    public const string Mouse = "mouse";
    public const string Delay = "delay";
    public const string Text = "text";
    public const string RepeatCount = "count";
    public const string RepeatWhileHeld = "whileHeld";
}

public static class MacroLimits
{
    public const int MaxSteps = 500;
    public const int MaxDelayMs = 60_000;
    public const int MaxRepeatCount = 100;
    public const int MaxWhileHeldLoops = 1_000;
    public const int MaxTextLength = 256;
}

public enum MacroOutcome { Completed, Cancelled, Empty }

public interface IMacroOutput
{
    void Send(PlaybackStep step);
}

public static class MacroRunner
{
    public static async Task<MacroOutcome> RunAsync(
        MacroDocument macro,
        IMacroOutput output,
        Func<int, CancellationToken, Task> delay,
        CancellationToken cancellation)
    {
        if (macro.Steps.Count == 0)
            return MacroOutcome.Empty;

        var loops = macro.RepeatMode == MacroKinds.RepeatWhileHeld
            ? MacroLimits.MaxWhileHeldLoops
            : Math.Clamp(macro.RepeatCount, 1, MacroLimits.MaxRepeatCount);
        var held = new List<PlaybackStep>();
        try
        {
            for (var loop = 0; loop < loops; loop++)
            {
                foreach (var step in macro.Steps)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (step.Kind == MacroKinds.Delay)
                    {
                        await delay(step.DelayMs, cancellation).ConfigureAwait(false);
                        continue;
                    }

                    var playback = ToPlayback(step);
                    if (playback is null)
                        continue;
                    output.Send(playback.Value);
                    Track(held, playback.Value);
                }
            }

            return MacroOutcome.Completed;
        }
        catch (OperationCanceledException)
        {
            return MacroOutcome.Cancelled;
        }
        finally
        {
            for (var index = held.Count - 1; index >= 0; index--)
                output.Send(held[index] with { Down = false });
        }
    }

    public static string? Validate(MacroDocument macro)
    {
        if (macro.RepeatMode is not (MacroKinds.RepeatCount or MacroKinds.RepeatWhileHeld))
            return $"A repetição da macro {macro.Name} é inválida.";
        if (macro.RepeatCount is < 1 or > MacroLimits.MaxRepeatCount)
            return $"A macro {macro.Name} precisa repetir de 1 a {MacroLimits.MaxRepeatCount} vezes.";
        if (macro.Steps.Count > MacroLimits.MaxSteps)
            return $"A macro {macro.Name} passa de {MacroLimits.MaxSteps} passos.";
        foreach (var step in macro.Steps)
        {
            var error = step.Kind switch
            {
                MacroKinds.Key when step.Code is < 1 or > 254 => "tem uma tecla inválida",
                MacroKinds.Mouse when step.Code is < 1 or > 5 => "tem um botão inválido",
                MacroKinds.Delay when step.DelayMs is < 0 or > MacroLimits.MaxDelayMs => "tem um atraso fora de 0 a 60000 ms",
                MacroKinds.Text when string.IsNullOrEmpty(step.Text) || step.Text.Length > MacroLimits.MaxTextLength
                    => "tem um texto vazio ou longo demais",
                MacroKinds.Text when step.Text.Any(char.IsControl) => "tem caracteres de controle no texto",
                MacroKinds.Key or MacroKinds.Mouse or MacroKinds.Delay or MacroKinds.Text => null,
                _ => "tem um passo de tipo desconhecido",
            };
            if (error is not null)
                return $"A macro {macro.Name} {error}.";
        }

        return null;
    }

    private static PlaybackStep? ToPlayback(MacroStepDocument step) => step.Kind switch
    {
        MacroKinds.Key => new PlaybackStep("key", step.Code, step.Down, null),
        MacroKinds.Mouse => new PlaybackStep("mouse", step.Code, step.Down, null),
        MacroKinds.Text => new PlaybackStep("text", 0, true, step.Text),
        _ => null,
    };

    private static void Track(List<PlaybackStep> held, PlaybackStep step)
    {
        if (step.Kind == "text")
            return;
        held.RemoveAll(item => item.Kind == step.Kind && item.Code == step.Code);
        if (step.Down)
            held.Add(step);
    }
}
