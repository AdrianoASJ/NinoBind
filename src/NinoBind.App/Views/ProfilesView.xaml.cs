using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NinoBind.Core.Configuration;
using NinoBind.Core.Profiles;
using NinoBind.Hosting;

namespace NinoBind.Views;

public partial class ProfilesView : UserControl
{
    private const string FileFilter = "Perfil NinoBind (*.ninobind.json)|*.ninobind.json|JSON (*.json)|*.json";
    private readonly AppHost _host;
    private readonly Func<string, string?> _activate;
    private readonly Action _changed;

    public ProfilesView(AppHost host, Func<string, string?> activate, Action changed)
    {
        _host = host;
        _activate = activate;
        _changed = changed;
        InitializeComponent();
        Reload();
    }

    public void Reload(string? selectId = null)
    {
        var document = _host.Configuration.Document;
        selectId ??= (ProfileList.SelectedItem as ProfileRow)?.Id;
        ProfileList.Items.Clear();
        foreach (var profile in document.Profiles)
        {
            var bindings = document.Bindings.Where(binding => binding.ProfileId == profile.Id).ToList();
            var macros = bindings.Select(binding => binding.MacroId).Where(id => id.Length > 0).Distinct().Count();
            var row = new ProfileRow(profile.Id, profile.Name, bindings.Count, macros, profile.Id == document.ActiveProfileId ? "Ativo" : "");
            ProfileList.Items.Add(row);
            if (row.Id == selectId)
                ProfileList.SelectedItem = row;
        }
    }

    private void ActivateRowClicked(object sender, RoutedEventArgs e)
    {
        SelectFromSender(sender);
        ActivateClicked(sender, e);
    }

    private void AddClicked(object sender, RoutedEventArgs e)
    {
        string? createdId = null;
        Apply(document =>
        {
            var result = ProfileService.Create(document, NameBox.Text);
            createdId = result.Profile?.Id;
            return result.Error;
        }, "Perfil criado.", () => createdId);
    }

    private void RenameClicked(object sender, RoutedEventArgs e)
    {
        SelectFromSender(sender);
        if (SelectedId() is not { } id)
            return;
        Apply(document => ProfileService.Rename(document, id, NameBox.Text), "Perfil renomeado.", () => id);
    }

    private void DuplicateClicked(object sender, RoutedEventArgs e)
    {
        SelectFromSender(sender);
        if (SelectedId() is not { } id)
            return;
        var name = NameBox.Text.Trim().Length > 0 && ProfileService.ValidateName(_host.Configuration.Document, NameBox.Text) is null
            ? NameBox.Text
            : ProfileService.AvailableName(_host.Configuration.Document, SelectedRow()!.Name + " cópia");
        string? createdId = null;
        Apply(document =>
        {
            var result = ProfileService.Duplicate(document, id, name);
            createdId = result.Profile?.Id;
            return result.Error;
        }, "Perfil duplicado com os bindings.", () => createdId);
    }

    private void DeleteClicked(object sender, RoutedEventArgs e)
    {
        SelectFromSender(sender);
        if (SelectedRow() is not { } row)
            return;
        var question = row.Bindings == 0
            ? $"Excluir o perfil {row.Name}?"
            : $"Excluir o perfil {row.Name} e os {row.Bindings} bindings dele? As macros continuam salvas.";
        if (MessageBox.Show(question, "NinoBind", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        Apply(document => ProfileService.Delete(document, row.Id), "Perfil excluído.", () => null);
    }

    private void ActivateClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedId() is not { } id)
            return;
        var error = _activate(id);
        StatusText.Text = error ?? "Perfil ativado.";
        Reload(id);
    }

    private void ExportClicked(object sender, RoutedEventArgs e)
    {
        SelectFromSender(sender);
        if (SelectedRow() is not { } row)
            return;
        var dialog = new SaveFileDialog
        {
            Filter = FileFilter,
            FileName = SafeFileName(row.Name) + ".ninobind.json",
            AddExtension = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            File.WriteAllText(dialog.FileName, ProfileService.Export(_host.Configuration.Document, row.Id));
            StatusText.Text = "Perfil exportado. O arquivo inclui os textos dos bindings e as macros usadas.";
            _host.Log.Info("profile exported");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = "Não foi possível exportar. " + exception.Message;
        }
    }

    private void ImportClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = FileFilter, CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        string json;
        try
        {
            if (new FileInfo(dialog.FileName).Length > ProfileService.MaxImportBytes)
            {
                StatusText.Text = "O arquivo passa de 1 MB.";
                return;
            }

            json = File.ReadAllText(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Não foi possível ler o arquivo. " + exception.Message;
            return;
        }

        string? importedId = null;
        Apply(document =>
        {
            var result = ProfileService.Import(document, json);
            importedId = result.Profile?.Id;
            return result.Error;
        }, "Perfil importado. Ele não foi ativado.", () => importedId);
        if (importedId is not null)
            _host.Log.Info("profile imported");
    }

    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedRow() is { } row)
            NameBox.Text = row.Name;
    }

    private void Apply(Func<ConfigurationDocument, string?> change, string success, Func<string?> select)
    {
        if (!_host.TryApply(change, out var error))
        {
            StatusText.Text = error ?? "A alteração não foi salva.";
            return;
        }

        StatusText.Text = success;
        _changed();
        Reload(select());
    }

    private void SelectFromSender(object sender)
    {
        if (sender is not FrameworkElement { Tag: string id })
            return;
        foreach (ProfileRow row in ProfileList.Items)
        {
            if (row.Id != id)
                continue;
            ProfileList.SelectedItem = row;
            break;
        }
    }

    private ProfileRow? SelectedRow() => ProfileList.SelectedItem as ProfileRow;

    private string? SelectedId() => SelectedRow()?.Id;

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return safe.Length == 0 ? "perfil" : safe;
    }

    private sealed record ProfileRow(string Id, string Name, int Bindings, int Macros, string State);
}
