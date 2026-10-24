using System.Text.Json;
using System.Text.Json.Serialization;

namespace NinoBind.Core.Configuration;

public enum ConfigurationLoadStatus
{
    Loaded,
    Created,
    Invalid,
}

public sealed record ConfigurationLoadResult(
    ConfigurationLoadStatus Status,
    ConfigurationDocument Document,
    string? Error);

public sealed class JsonConfigurationStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public ConfigurationLoadResult LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            var created = ConfigurationDocument.CreateDefault();
            Save(path, created);
            return new ConfigurationLoadResult(ConfigurationLoadStatus.Created, created, null);
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Invalid(exception.Message);
        }

        ConfigurationDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ConfigurationDocument>(text, Options);
        }
        catch (JsonException exception)
        {
            return Invalid(exception.Message);
        }

        var error = ConfigurationValidator.Validate(document);
        if (error is not null || document is null)
            return Invalid(error ?? "O documento está vazio.");

        return new ConfigurationLoadResult(ConfigurationLoadStatus.Loaded, document, null);
    }

    public void Save(string path, ConfigurationDocument document)
    {
        var error = ConfigurationValidator.Validate(document);
        if (error is not null)
            throw new InvalidOperationException(error);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("O caminho da configuração não tem pasta.");

        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        var json = JsonSerializer.Serialize(document, Options);
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    private static ConfigurationLoadResult Invalid(string error) =>
        new(ConfigurationLoadStatus.Invalid, ConfigurationDocument.CreateDefault(), error);
}
