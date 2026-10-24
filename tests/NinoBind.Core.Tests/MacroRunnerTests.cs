using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;
using NinoBind.Core.Macros;
using Xunit;

namespace NinoBind.Core.Tests;

public sealed class MacroRunnerTests
{
    [Fact]
    public async Task Steps_play_in_order_and_delays_are_awaited()
    {
        var output = new RecordingOutput();
        var delays = new List<int>();
        var macro = Macro(KeyStep(0x41, true), Delay(50), KeyStep(0x41, false));

        var outcome = await MacroRunner.RunAsync(macro, output, (ms, _) => { delays.Add(ms); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(MacroOutcome.Completed, outcome);
        Assert.Equal([new PlaybackStep("key", 0x41, true, null), new PlaybackStep("key", 0x41, false, null)], output.Steps);
        Assert.Equal([50], delays);
    }

    [Fact]
    public async Task Repeat_count_runs_the_sequence_that_many_times()
    {
        var output = new RecordingOutput();
        var macro = Macro(KeyStep(0x41, true), KeyStep(0x41, false));
        macro.RepeatCount = 3;

        await MacroRunner.RunAsync(macro, output, NoDelay, CancellationToken.None);

        Assert.Equal(6, output.Steps.Count);
    }

    [Fact]
    public async Task Cancellation_releases_keys_the_macro_still_holds()
    {
        var output = new RecordingOutput();
        using var cancellation = new CancellationTokenSource();
        var macro = Macro(KeyStep(0xA2, true), MouseStep(1, true), Delay(1000), MouseStep(1, false), KeyStep(0xA2, false));

        var outcome = await MacroRunner.RunAsync(macro, output, (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, cancellation.Token);

        Assert.Equal(MacroOutcome.Cancelled, outcome);
        Assert.Equal(
            [
                new PlaybackStep("key", 0xA2, true, null),
                new PlaybackStep("mouse", 1, true, null),
                new PlaybackStep("mouse", 1, false, null),
                new PlaybackStep("key", 0xA2, false, null),
            ],
            output.Steps);
    }

    [Fact]
    public async Task While_held_stops_at_the_loop_limit()
    {
        var output = new RecordingOutput();
        var macro = Macro(new MacroStepDocument { Kind = MacroKinds.Text, Text = "x" });
        macro.RepeatMode = MacroKinds.RepeatWhileHeld;

        await MacroRunner.RunAsync(macro, output, NoDelay, CancellationToken.None);

        Assert.Equal(MacroLimits.MaxWhileHeldLoops, output.Steps.Count);
    }

    [Fact]
    public void Limits_are_validated()
    {
        Assert.NotNull(MacroRunner.Validate(Macro(Delay(MacroLimits.MaxDelayMs + 1))));
        var repeated = Macro(KeyStep(0x41, true));
        repeated.RepeatCount = MacroLimits.MaxRepeatCount + 1;
        Assert.NotNull(MacroRunner.Validate(repeated));
        var longMacro = Macro(Enumerable.Range(0, MacroLimits.MaxSteps + 1).Select(_ => Delay(0)).ToArray());
        Assert.NotNull(MacroRunner.Validate(longMacro));
        Assert.Null(MacroRunner.Validate(Macro(KeyStep(0x41, true), Delay(10), KeyStep(0x41, false))));
    }

    [Fact]
    public void Macro_binding_runs_only_when_the_macro_has_steps()
    {
        var document = ConfigurationDocument.CreateDefault();
        var macro = Macro();
        document.Macros.Add(macro);
        document.Bindings.Add(new BindingDocument
        {
            Id = "b1",
            ProfileId = "default",
            InputKind = BindingKinds.Key,
            InputCode = 0x70,
            ActionKind = BindingKinds.Macro,
            MacroId = macro.Id,
            IsEnabled = true,
        });

        Assert.Equal(BindingMatch.None, BindingEngine.Resolve(document, BindingKinds.Key, 0x70).Match);
        Assert.Equal("Macro vazia", BindingEngine.Status(document, document.Bindings[0]));

        macro.Steps.Add(KeyStep(0x41, true));
        Assert.Equal(BindingMatch.Ready, BindingEngine.Resolve(document, BindingKinds.Key, 0x70).Match);
        Assert.Equal("Ativo", BindingEngine.Status(document, document.Bindings[0]));
    }

    [Fact]
    public void Pause_is_reserved_for_the_emergency_stop()
    {
        var document = ConfigurationDocument.CreateDefault();
        document.Bindings.Add(new BindingDocument
        {
            Id = "b1",
            ProfileId = "default",
            InputKind = BindingKinds.Key,
            InputCode = BindingEngine.ReservedKey,
            ActionKind = BindingKinds.Suppress,
            IsEnabled = true,
        });

        Assert.NotNull(ConfigurationValidator.Validate(document));
        Assert.Equal(BindingMatch.None, BindingEngine.Resolve(document, BindingKinds.Key, BindingEngine.ReservedKey).Match);

        document.Bindings[0].InputCode = BindingEngine.ReservedScrollLockKey;
        Assert.Contains("Scroll Lock", ConfigurationValidator.Validate(document));
        Assert.Equal(BindingMatch.None, BindingEngine.Resolve(document, BindingKinds.Key, BindingEngine.ReservedScrollLockKey).Match);
    }

    private static Task NoDelay(int milliseconds, CancellationToken cancellation) => Task.CompletedTask;

    private static MacroDocument Macro(params MacroStepDocument[] steps) => new()
    {
        Id = "m1",
        Name = "Teste",
        Steps = [.. steps],
    };

    private static MacroStepDocument KeyStep(int code, bool down) => new() { Kind = MacroKinds.Key, Code = code, Down = down };

    private static MacroStepDocument MouseStep(int code, bool down) => new() { Kind = MacroKinds.Mouse, Code = code, Down = down };

    private static MacroStepDocument Delay(int milliseconds) => new() { Kind = MacroKinds.Delay, DelayMs = milliseconds };

    private sealed class RecordingOutput : IMacroOutput
    {
        public List<PlaybackStep> Steps { get; } = [];

        public void Send(PlaybackStep step) => Steps.Add(step);
    }
}
