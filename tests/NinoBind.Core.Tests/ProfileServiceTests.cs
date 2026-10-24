using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;
using NinoBind.Core.Macros;
using NinoBind.Core.Profiles;
using Xunit;

namespace NinoBind.Core.Tests;

public sealed class ProfileServiceTests
{
    [Fact]
    public void Names_must_be_unique_and_not_empty()
    {
        var document = ConfigurationDocument.CreateDefault();

        Assert.NotNull(ProfileService.Create(document, "  ").Error);
        Assert.NotNull(ProfileService.Create(document, "default").Error);
        Assert.Null(ProfileService.Create(document, "Jogo").Error);
        Assert.Null(ConfigurationValidator.Validate(document));
    }

    [Fact]
    public void Duplicate_copies_bindings_with_new_ids()
    {
        var document = Sample();

        var copy = ProfileService.Duplicate(document, "default", "Cópia").Profile!;

        var copied = document.Bindings.Where(binding => binding.ProfileId == copy.Id).ToList();
        Assert.Single(copied);
        Assert.NotEqual("b1", copied[0].Id);
        Assert.Equal("m1", copied[0].MacroId);
        Assert.Null(ConfigurationValidator.Validate(document));
    }

    [Fact]
    public void Active_or_last_profile_cannot_be_deleted()
    {
        var document = Sample();
        Assert.NotNull(ProfileService.Delete(document, "default"));

        var other = ProfileService.Create(document, "Outro").Profile!;
        ProfileService.Activate(document, other.Id);
        Assert.Null(ProfileService.Delete(document, "default"));
        Assert.DoesNotContain(document.Bindings, binding => binding.ProfileId == "default");
        Assert.Single(document.Macros);
    }

    [Fact]
    public void Export_then_import_creates_an_inactive_copy_with_its_macro()
    {
        var document = Sample();
        var json = ProfileService.Export(document, "default");

        var imported = ProfileService.Import(document, json);

        Assert.Null(imported.Error);
        Assert.Equal("Default 2", imported.Profile!.Name);
        Assert.Equal("default", document.ActiveProfileId);
        Assert.Equal(2, document.Macros.Count);
        var binding = document.Bindings.Single(item => item.ProfileId == imported.Profile.Id);
        Assert.NotEqual("m1", binding.MacroId);
        Assert.Contains(document.Macros, macro => macro.Id == binding.MacroId);
        Assert.Null(ConfigurationValidator.Validate(document));
    }

    [Fact]
    public void Invalid_import_leaves_the_document_unchanged()
    {
        var document = Sample();
        var export = System.Text.Json.JsonSerializer.Deserialize<ProfileExportDocument>(
            ProfileService.Export(document, "default"),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        export.Bindings[0].InputCode = 999;
        var json = System.Text.Json.JsonSerializer.Serialize(export);

        Assert.NotNull(ProfileService.Import(document, json).Error);
        Assert.NotNull(ProfileService.Import(document, "{}").Error);
        Assert.NotNull(ProfileService.Import(document, "não é json").Error);
        Assert.Single(document.Profiles);
        Assert.Single(document.Bindings);
        Assert.Single(document.Macros);
    }

    [Fact]
    public void Restore_from_snapshot_undoes_changes_in_place()
    {
        var document = Sample();
        var snapshot = document.DeepCopy();

        ProfileService.Create(document, "Temporário");
        document.Bindings.Clear();
        document.RestoreFrom(snapshot);

        Assert.Single(document.Profiles);
        Assert.Single(document.Bindings);
    }

    private static ConfigurationDocument Sample()
    {
        var document = ConfigurationDocument.CreateDefault();
        document.Macros.Add(new MacroDocument
        {
            Id = "m1",
            Name = "Salvar",
            Steps = [new MacroStepDocument { Kind = MacroKinds.Key, Code = 0x41, Down = true }],
        });
        document.Bindings.Add(new BindingDocument
        {
            Id = "b1",
            ProfileId = "default",
            InputKind = BindingKinds.Key,
            InputCode = 0x70,
            ActionKind = BindingKinds.Macro,
            MacroId = "m1",
            IsEnabled = true,
        });
        return document;
    }
}
