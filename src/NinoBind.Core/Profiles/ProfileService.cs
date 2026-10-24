using System.Text.Json;
using System.Text.Json.Serialization;
using NinoBind.Core.Configuration;

namespace NinoBind.Core.Profiles;

public sealed record ProfileResult(ProfileDocument? Profile, string? Error)
{
    public static ProfileResult Fail(string error) => new(null, error);
}

public sealed class ProfileExportDocument
{
    public const string FormatName = "ninobind-profile";

    public string Format { get; set; } = "";

    public int SchemaVersion { get; set; }

    public ProfileDocument Profile { get; set; } = new();

    public List<BindingDocument> Bindings { get; set; } = [];

    public List<MacroDocument> Macros { get; set; } = [];
}

public static class ProfileService
{
    public const int MaxNameLength = 60;
    public const long MaxImportBytes = 1_048_576;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string? ValidateName(ConfigurationDocument document, string name, string? exceptId = null)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return "Dê um nome ao perfil.";
        if (trimmed.Length > MaxNameLength)
            return $"O nome passa de {MaxNameLength} caracteres.";
        if (trimmed.Any(char.IsControl))
            return "O nome não pode ter caracteres de controle.";
        return document.Profiles.Any(profile => profile.Id != exceptId && SameName(profile.Name, trimmed))
            ? "Já existe um perfil com esse nome."
            : null;
    }

    public static ProfileResult Create(ConfigurationDocument document, string name)
    {
        var error = ValidateName(document, name);
        if (error is not null)
            return ProfileResult.Fail(error);
        var profile = new ProfileDocument { Id = NewId(), Name = name.Trim(), IsEnabled = true };
        document.Profiles.Add(profile);
        return new ProfileResult(profile, null);
    }

    public static string? Rename(ConfigurationDocument document, string profileId, string name)
    {
        var profile = Find(document, profileId);
        if (profile is null)
            return "O perfil não existe.";
        var error = ValidateName(document, name, profileId);
        if (error is not null)
            return error;
        profile.Name = name.Trim();
        return null;
    }

    public static ProfileResult Duplicate(ConfigurationDocument document, string sourceId, string name)
    {
        if (Find(document, sourceId) is null)
            return ProfileResult.Fail("O perfil de origem não existe.");
        var created = Create(document, name);
        if (created.Profile is null)
            return created;
        var copies = document.Bindings
            .Where(binding => binding.ProfileId == sourceId)
            .Select(binding => CopyBinding(binding, NewId(), created.Profile.Id, binding.MacroId))
            .ToList();
        document.Bindings.AddRange(copies);
        return created;
    }

    public static string? Delete(ConfigurationDocument document, string profileId)
    {
        if (Find(document, profileId) is null)
            return "O perfil não existe.";
        if (document.ActiveProfileId == profileId)
            return "Ative outro perfil antes de excluir este.";
        if (document.Profiles.Count == 1)
            return "O arquivo precisa de pelo menos um perfil.";
        document.Profiles.RemoveAll(profile => profile.Id == profileId);
        document.Bindings.RemoveAll(binding => binding.ProfileId == profileId);
        return null;
    }

    public static string? Activate(ConfigurationDocument document, string profileId)
    {
        if (Find(document, profileId) is null)
            return "O perfil não existe.";
        document.ActiveProfileId = profileId;
        return null;
    }

    public static string Export(ConfigurationDocument document, string profileId)
    {
        var profile = Find(document, profileId)
            ?? throw new InvalidOperationException("O perfil não existe.");
        var bindings = document.Bindings.Where(binding => binding.ProfileId == profileId).ToList();
        var macroIds = bindings.Select(binding => binding.MacroId).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
        var export = new ProfileExportDocument
        {
            Format = ProfileExportDocument.FormatName,
            SchemaVersion = Schema.CurrentVersion,
            Profile = profile,
            Bindings = bindings,
            Macros = document.Macros.Where(macro => macroIds.Contains(macro.Id)).ToList(),
        };
        return JsonSerializer.Serialize(export, Options);
    }

    public static ProfileResult Import(ConfigurationDocument document, string json)
    {
        if (json.Length > MaxImportBytes)
            return ProfileResult.Fail("O arquivo passa de 1 MB.");

        ProfileExportDocument? imported;
        try
        {
            imported = JsonSerializer.Deserialize<ProfileExportDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            return ProfileResult.Fail("O arquivo não é um JSON válido. " + exception.Message);
        }

        if (imported is null || imported.Format != ProfileExportDocument.FormatName)
            return ProfileResult.Fail("O arquivo não é um perfil exportado pelo NinoBind.");
        if (imported.SchemaVersion != Schema.CurrentVersion)
            return ProfileResult.Fail($"A versão {imported.SchemaVersion} do perfil não é suportada.");
        if (imported.Profile is null || imported.Bindings is null || imported.Macros is null)
            return ProfileResult.Fail("O arquivo está incompleto.");

        var candidate = document.DeepCopy();
        var profile = new ProfileDocument
        {
            Id = NewId(),
            Name = AvailableName(candidate, imported.Profile.Name),
            IsEnabled = true,
        };
        candidate.Profiles.Add(profile);

        var macroIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var macro in imported.Macros)
        {
            if (macro is null || string.IsNullOrEmpty(macro.Id) || macroIds.ContainsKey(macro.Id))
                return ProfileResult.Fail("O arquivo tem uma macro sem identificador ou repetida.");
            var newId = NewId();
            macroIds[macro.Id] = newId;
            candidate.Macros.Add(new MacroDocument
            {
                Id = newId,
                Name = macro.Name,
                RepeatMode = macro.RepeatMode,
                RepeatCount = macro.RepeatCount,
                Steps = macro.Steps ?? [],
            });
        }

        foreach (var binding in imported.Bindings)
        {
            if (binding is null)
                return ProfileResult.Fail("O arquivo tem um binding vazio.");
            var macroId = binding.MacroId.Length == 0 ? "" : macroIds.GetValueOrDefault(binding.MacroId, "");
            candidate.Bindings.Add(CopyBinding(binding, NewId(), profile.Id, macroId));
        }

        var error = ConfigurationValidator.Validate(candidate);
        if (error is not null)
            return ProfileResult.Fail(error);

        document.Profiles.Add(profile);
        document.Macros.AddRange(candidate.Macros.Where(macro => macroIds.ContainsValue(macro.Id)));
        document.Bindings.AddRange(candidate.Bindings.Where(binding => binding.ProfileId == profile.Id));
        return new ProfileResult(profile, null);
    }

    public static string AvailableName(ConfigurationDocument document, string? wanted)
    {
        var baseName = string.IsNullOrWhiteSpace(wanted) ? "Perfil importado" : wanted.Trim();
        if (baseName.Length > MaxNameLength - 4)
            baseName = baseName[..(MaxNameLength - 4)];
        var name = baseName;
        for (var suffix = 2; document.Profiles.Any(profile => SameName(profile.Name, name)); suffix++)
            name = $"{baseName} {suffix}";
        return name;
    }

    private static ProfileDocument? Find(ConfigurationDocument document, string profileId) =>
        document.Profiles.FirstOrDefault(profile => profile.Id == profileId);

    private static bool SameName(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static BindingDocument CopyBinding(BindingDocument source, string id, string profileId, string macroId) => new()
    {
        Id = id,
        ProfileId = profileId,
        DeviceId = source.DeviceId,
        DeviceLabel = source.DeviceLabel,
        InputKind = source.InputKind,
        InputCode = source.InputCode,
        Input = source.Input,
        ActionKind = source.ActionKind,
        Action = source.Action,
        OutputCode = source.OutputCode,
        Chord = [.. source.Chord ?? []],
        Text = source.Text ?? "",
        MacroId = macroId,
        IsEnabled = source.IsEnabled,
    };
}
