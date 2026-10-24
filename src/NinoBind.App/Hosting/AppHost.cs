using System.IO;
using NinoBind.Core.Configuration;

namespace NinoBind.Hosting;

public sealed class FileLog
{
    private readonly string _path;

    public FileLog(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public void Info(string message) => Write("info", message);

    public void Error(string message) => Write("error", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} {level} {message}{Environment.NewLine}";
        File.AppendAllText(_path, line);
    }
}

public sealed class AppHost
{
    private AppHost(string root, FileLog log, ConfigurationLoadResult configuration)
    {
        Root = root;
        Log = log;
        Configuration = configuration;
    }

    public string Root { get; }

    public string ConfigurationPath => Path.Combine(Root, "config.json");

    public string LogPath => Path.Combine(Root, "logs", "ninobind.log");

    public FileLog Log { get; }

    public ConfigurationLoadResult Configuration { get; }

    public OverviewSnapshot Overview =>
        OverviewSnapshot.From(Configuration.Document, EngineState.NotStarted);

    public static AppHost Start()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NinoBind");
        Directory.CreateDirectory(root);
        var log = new FileLog(Path.Combine(root, "logs", "ninobind.log"));
        var store = new JsonConfigurationStore();
        var configuration = store.LoadOrCreate(Path.Combine(root, "config.json"));
        log.Info($"config {configuration.Status} schema {configuration.Document.SchemaVersion}");
        if (configuration.Error is not null)
            log.Error(configuration.Error);
        return new AppHost(root, log, configuration);
    }

    public bool TryApply(Func<ConfigurationDocument, string?> change, out string? error)
    {
        var document = Configuration.Document;
        var snapshot = document.DeepCopy();
        error = change(document) ?? ConfigurationValidator.Validate(document);
        if (error is null && TrySave(out error))
            return true;
        document.RestoreFrom(snapshot);
        return false;
    }

    public bool TrySave(out string? error)
    {
        if (Configuration.Status == ConfigurationLoadStatus.Invalid)
        {
            error = "O arquivo existente é inválido e não foi alterado.";
            return false;
        }

        try
        {
            new JsonConfigurationStore().Save(ConfigurationPath, Configuration.Document);
            Log.Info($"bindings {Configuration.Document.Bindings.Count}");
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error = exception.Message;
            return false;
        }
    }
}
