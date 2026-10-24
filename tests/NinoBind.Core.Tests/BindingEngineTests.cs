using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;
using Xunit;

namespace NinoBind.Core.Tests;

public sealed class BindingEngineTests
{
    [Fact]
    public void Global_key_binding_is_ready()
    {
        var document = Sample();
        document.Bindings.Add(KeyBinding("b1", deviceId: "", input: 0x70, output: 0x1B));

        var result = BindingEngine.Resolve(document, BindingKinds.Key, 0x70);

        Assert.Equal(BindingMatch.Ready, result.Match);
        Assert.Equal([new PlaybackStep("key", 0x1B, true, null)], BindingEngine.Press(result.Binding!));
        Assert.Equal([new PlaybackStep("key", 0x1B, false, null)], BindingEngine.Release(result.Binding!));
    }

    [Fact]
    public void Device_specific_binding_is_not_applied_by_the_global_hook()
    {
        var document = Sample();
        document.Bindings.Add(KeyBinding("b1", deviceId: @"HID\VID_1", input: 0x70, output: 0x1B));

        var result = BindingEngine.Resolve(document, BindingKinds.Key, 0x70);

        Assert.Equal(BindingMatch.None, result.Match);
        Assert.Equal("Não aplicado", BindingEngine.Status(document, document.Bindings[0]));
    }

    [Fact]
    public void Overlapping_enabled_bindings_are_a_conflict_and_do_not_run()
    {
        var document = Sample();
        document.Bindings.Add(KeyBinding("any", deviceId: "", input: 0x70, output: 0x1B));
        document.Bindings.Add(KeyBinding("desk", deviceId: @"HID\VID_1", input: 0x70, output: 0x41));

        var result = BindingEngine.Resolve(document, BindingKinds.Key, 0x70);

        Assert.Equal(BindingMatch.Conflict, result.Match);
        Assert.Equal("Conflito", BindingEngine.Status(document, document.Bindings[0]));
    }

    [Fact]
    public void Disabled_and_other_profile_bindings_do_not_match()
    {
        var document = Sample();
        document.Profiles.Add(new ProfileDocument { Id = "work", Name = "Work", IsEnabled = true });
        var disabled = KeyBinding("off", "", 0x70, 0x1B);
        disabled.IsEnabled = false;
        var other = KeyBinding("work", "", 0x70, 0x41);
        other.ProfileId = "work";
        document.Bindings.Add(disabled);
        document.Bindings.Add(other);

        Assert.Equal(BindingMatch.None, BindingEngine.Resolve(document, BindingKinds.Key, 0x70).Match);
    }

    [Fact]
    public void Chord_release_is_the_reverse_of_the_press()
    {
        var binding = new BindingDocument
        {
            ActionKind = BindingKinds.Chord,
            Chord = [0xA2, 0xA0, 0x53],
        };

        Assert.Equal([0xA2, 0xA0, 0x53], BindingEngine.Press(binding).Select(step => step.Code));
        Assert.Equal([0x53, 0xA0, 0xA2], BindingEngine.Release(binding).Select(step => step.Code));
    }

    [Fact]
    public void Text_is_emitted_only_on_press_and_held_keys_can_all_be_released()
    {
        var binding = new BindingDocument { ActionKind = BindingKinds.Text, Text = "Oi" };
        var held = new HeldOutputs();
        held.Hold("key:70", BindingEngine.Release(new BindingDocument { ActionKind = BindingKinds.Key, OutputCode = 0x1B }));

        Assert.Equal("Oi", BindingEngine.Press(binding).Single().Text);
        Assert.Empty(BindingEngine.Release(binding));
        Assert.Single(held.TakeAll());
        Assert.Empty(held.Take("key:70"));
    }

    private static ConfigurationDocument Sample() => ConfigurationDocument.CreateDefault();

    private static BindingDocument KeyBinding(string id, string deviceId, int input, int output) => new()
    {
        Id = id,
        ProfileId = "default",
        DeviceId = deviceId,
        InputKind = BindingKinds.Key,
        InputCode = input,
        ActionKind = BindingKinds.Key,
        OutputCode = output,
        IsEnabled = true,
    };
}
