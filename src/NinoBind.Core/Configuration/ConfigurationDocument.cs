namespace NinoBind.Core.Configuration;

public static class Schema
{
    public const int CurrentVersion = 1;
}

public sealed class ProfileDocument
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public bool IsEnabled { get; set; } = true;
}

public sealed class BindingDocument
{
    public string Id { get; set; } = "";

    public string ProfileId { get; set; } = "";

    public string DeviceId { get; set; } = "";

    public string DeviceLabel { get; set; } = "";

    public string InputKind { get; set; } = "key";

    public int InputCode { get; set; }

    public string Input { get; set; } = "";

    public string ActionKind { get; set; } = "none";

    public string Action { get; set; } = "";

    public int OutputCode { get; set; }

    public List<int> Chord { get; set; } = [];

    public string Text { get; set; } = "";

    public string MacroId { get; set; } = "";

    public bool IsEnabled { get; set; }
}

public sealed class MacroStepDocument
{
    public string Kind { get; set; } = "delay";

    public int Code { get; set; }

    public bool Down { get; set; }

    public int DelayMs { get; set; }

    public string Text { get; set; } = "";
}

public sealed class MacroDocument
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string RepeatMode { get; set; } = "count";

    public int RepeatCount { get; set; } = 1;

    public List<MacroStepDocument> Steps { get; set; } = [];
}

public sealed class SettingsDocument
{
    public string Language { get; set; } = "pt-BR";

    public bool CloseToTray { get; set; }

    public bool StartEngineOnLaunch { get; set; }
}

public sealed class ConfigurationDocument
{
    public int SchemaVersion { get; set; } = Schema.CurrentVersion;

    public string ActiveProfileId { get; set; } = "";

    public List<ProfileDocument> Profiles { get; set; } = [];

    public List<BindingDocument> Bindings { get; set; } = [];

    public List<MacroDocument> Macros { get; set; } = [];

    public SettingsDocument Settings { get; set; } = new();

    public ConfigurationDocument DeepCopy() =>
        System.Text.Json.JsonSerializer.Deserialize<ConfigurationDocument>(
            System.Text.Json.JsonSerializer.Serialize(this))!;

    public void RestoreFrom(ConfigurationDocument snapshot)
    {
        var copy = snapshot.DeepCopy();
        SchemaVersion = copy.SchemaVersion;
        ActiveProfileId = copy.ActiveProfileId;
        Profiles = copy.Profiles;
        Bindings = copy.Bindings;
        Macros = copy.Macros;
        Settings = copy.Settings;
    }

    public static ConfigurationDocument CreateDefault()
    {
        var profile = new ProfileDocument
        {
            Id = "default",
            Name = "Default",
            IsEnabled = true,
        };
        return new ConfigurationDocument
        {
            SchemaVersion = Schema.CurrentVersion,
            ActiveProfileId = profile.Id,
            Profiles = [profile],
            Settings = new SettingsDocument(),
        };
    }
}
