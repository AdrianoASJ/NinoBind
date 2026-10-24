using NinoBind.Core.Configuration;
using Xunit;

namespace NinoBind.Core.Tests;

public sealed class OverviewSnapshotTests
{
    [Fact]
    public void Default_document_reports_real_empty_state()
    {
        var snapshot = OverviewSnapshot.From(ConfigurationDocument.CreateDefault());

        Assert.False(snapshot.DeviceDetectionRan);
        Assert.Equal(0, snapshot.DetectedDeviceCount);
        Assert.Equal("Default", snapshot.ActiveProfileName);
        Assert.Equal(0, snapshot.ActiveBindingCount);
        Assert.Equal(0, snapshot.MacroCount);
        Assert.Equal(EngineState.NotStarted, snapshot.Engine);
    }

    [Fact]
    public void Active_bindings_count_only_enabled_bindings_of_the_active_profile()
    {
        var document = ConfigurationDocument.CreateDefault();
        document.Profiles.Add(new ProfileDocument { Id = "work", Name = "Work", IsEnabled = true });
        document.Bindings.Add(new BindingDocument
        {
            Id = "b1",
            ProfileId = "default",
            Input = "F1",
            Action = "Ctrl+S",
            IsEnabled = true,
        });
        document.Bindings.Add(new BindingDocument
        {
            Id = "b2",
            ProfileId = "default",
            Input = "F2",
            Action = "Esc",
            IsEnabled = false,
        });
        document.Bindings.Add(new BindingDocument
        {
            Id = "b3",
            ProfileId = "work",
            Input = "F3",
            Action = "Ctrl+C",
            IsEnabled = true,
        });
        document.Macros.Add(new MacroDocument { Id = "m1", Name = "Quick Save" });

        var snapshot = OverviewSnapshot.From(document);

        Assert.Equal(1, snapshot.ActiveBindingCount);
        Assert.Equal(1, snapshot.MacroCount);
    }
}
