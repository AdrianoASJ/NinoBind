using NinoBind.Core.Configuration;
using Xunit;

namespace NinoBind.Core.Tests;

public sealed class JsonConfigurationStoreTests
{
    [Fact]
    public void Missing_file_is_created_and_reloaded()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ninobind-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "config.json");
        var store = new JsonConfigurationStore();

        var created = store.LoadOrCreate(path);
        var loaded = store.LoadOrCreate(path);

        Assert.Equal(ConfigurationLoadStatus.Created, created.Status);
        Assert.Equal(ConfigurationLoadStatus.Loaded, loaded.Status);
        Assert.Equal("Default", loaded.Document.Profiles[0].Name);
        Assert.Equal("pt-BR", loaded.Document.Settings.Language);
    }

    [Fact]
    public void Invalid_file_is_preserved()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ninobind-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, "{");
        var store = new JsonConfigurationStore();

        var result = store.LoadOrCreate(path);

        Assert.Equal(ConfigurationLoadStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal("{", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Unsupported_schema_is_rejected_without_rewrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ninobind-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        const string original = """{"schemaVersion":99,"activeProfileId":"default","profiles":[{"id":"default","name":"Default","isEnabled":true}],"bindings":[],"macros":[],"settings":{"language":"pt-BR"}}""";
        File.WriteAllText(path, original);
        var store = new JsonConfigurationStore();

        var result = store.LoadOrCreate(path);

        Assert.Equal(ConfigurationLoadStatus.Invalid, result.Status);
        Assert.Equal(original, File.ReadAllText(path));
    }
}
