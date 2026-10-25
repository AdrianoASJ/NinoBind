namespace NinoBind.Core.Configuration;

public enum EngineState
{
    NotStarted,
    Running,
    Paused,
}

public sealed record OverviewSnapshot(
    bool DeviceDetectionRan,
    int DetectedDeviceCount,
    string ActiveProfileName,
    int ActiveBindingCount,
    int TotalBindingCount,
    int MacroCount,
    EngineState Engine)
{
    public static OverviewSnapshot From(
        ConfigurationDocument document,
        EngineState engine = EngineState.NotStarted,
        bool deviceDetectionRan = false,
        int detectedDeviceCount = 0)
    {
        var profile = document.Profiles.FirstOrDefault(item => item.Id == document.ActiveProfileId);
        var activeBindings = document.Bindings.Count(item =>
            item.IsEnabled && item.ProfileId == document.ActiveProfileId);
        return new OverviewSnapshot(
            DeviceDetectionRan: deviceDetectionRan,
            DetectedDeviceCount: detectedDeviceCount,
            ActiveProfileName: profile?.Name ?? "",
            ActiveBindingCount: activeBindings,
            TotalBindingCount: document.Bindings.Count,
            MacroCount: document.Macros.Count,
            Engine: engine);
    }
}
