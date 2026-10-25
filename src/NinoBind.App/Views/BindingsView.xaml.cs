using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NinoBind.Core.Bindings;
using NinoBind.Core.Configuration;
using NinoBind.Hosting;
using NinoBind.Input;

namespace NinoBind.Views;

public partial class BindingsView : UserControl
{
    private readonly AppHost _host;
    private readonly DesktopInputSession _session;
    private readonly BindingController _controller;
    private readonly nint _window;
    private readonly Action _saved;

    public BindingsView(
        AppHost host,
        DesktopInputSession session,
        BindingController controller,
        nint window,
        Action saved)
    {
        _host = host;
        _session = session;
        _controller = controller;
        _window = window;
        _saved = saved;
        InitializeComponent();
        DeviceFilter.Items.Add("Todos os dispositivos");
        DeviceFilter.Items.Add("Qualquer dispositivo");
        foreach (var device in _session.Devices)
            DeviceFilter.Items.Add(DeviceLabels.Friendly(device, _session.Devices, _host.Configuration.Document.Settings.DeviceNicknames));
        DeviceFilter.SelectedIndex = 0;
        Reload();
    }

    public void Reload()
    {
        var document = _host.Configuration.Document;
        RefreshProfileFilter(document);
        var search = SearchBox.Text.Trim();
        var deviceFilter = DeviceFilter.SelectedItem as string;
        var profileFilter = ProfileFilter.SelectedItem as string;
        BindingList.Items.Clear();
        foreach (var binding in document.Bindings)
        {
            var profile = document.Profiles.FirstOrDefault(item => item.Id == binding.ProfileId)?.Name ?? binding.ProfileId;
            var device = string.IsNullOrEmpty(binding.DeviceId)
                ? "Qualquer dispositivo"
                : _session.Devices.FirstOrDefault(item => item.DevicePath == binding.DeviceId) is { } found
                    ? DeviceLabels.Friendly(found, _session.Devices, document.Settings.DeviceNicknames)
                    : binding.DeviceLabel;
            var input = binding.InputKind == BindingKinds.Mouse
                ? InputNames.MouseButton(binding.InputCode)
                : InputNames.Key((ushort)binding.InputCode);
            var action = DescribeFull(document, binding);
            if (!string.IsNullOrEmpty(search)
                && !device.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !input.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !action.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !profile.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            if (deviceFilter == "Qualquer dispositivo" && !string.IsNullOrEmpty(binding.DeviceId))
                continue;
            if (deviceFilter is not null && deviceFilter is not ("Todos os dispositivos" or "Qualquer dispositivo") && device != deviceFilter)
                continue;
            if (profileFilter is not null && profileFilter != "Todos os perfis" && profile != profileFilter)
                continue;
            BindingList.Items.Add(new BindingRow(
                binding.Id,
                device,
                string.IsNullOrEmpty(binding.DeviceId) ? "\uE71B" : binding.InputKind == BindingKinds.Mouse ? "\uE962" : "\uE92E",
                input,
                binding.InputKind == BindingKinds.Mouse ? "\uE962" : "\uE92E",
                action,
                profile,
                binding.IsEnabled));
        }

        var empty = document.Bindings.Count == 0;
        EmptyPanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        BindingList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        AddButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Text = empty
            ? ""
            : $"{BindingList.Items.Count} de {document.Bindings.Count} {(document.Bindings.Count == 1 ? "binding" : "bindings")}";
        if (!empty)
            BindingList.Height = 40 + BindingList.Items.Count * 56;
    }

    private bool _refreshingFilter;

    private void RefreshProfileFilter(ConfigurationDocument document)
    {
        var wanted = new[] { "Todos os perfis" }.Concat(document.Profiles.Select(profile => profile.Name)).ToList();
        if (ProfileFilter.Items.Cast<string>().SequenceEqual(wanted))
            return;
        var selected = ProfileFilter.SelectedItem as string;
        _refreshingFilter = true;
        ProfileFilter.Items.Clear();
        foreach (var name in wanted)
            ProfileFilter.Items.Add(name);
        ProfileFilter.SelectedItem = selected is not null && wanted.Contains(selected) ? selected : wanted[0];
        _refreshingFilter = false;
    }

    private void AddClicked(object sender, RoutedEventArgs e) => Edit(null);

    private void RowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
            return;
        Edit(Selected());
    }

    private void EditRowClicked(object sender, RoutedEventArgs e) => Edit(FromSender(sender));

    private void DuplicateRowClicked(object sender, RoutedEventArgs e)
    {
        var source = FromSender(sender);
        if (source is null)
            return;
        var copy = Clone(source);
        copy.Id = Guid.NewGuid().ToString("N");
        Commit(copy);
    }

    private void DeleteRowClicked(object sender, RoutedEventArgs e)
    {
        var binding = FromSender(sender);
        if (binding is null)
            return;
        if (MessageBox.Show("Excluir este binding?", "NinoBind", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _host.Configuration.Document.Bindings.Remove(binding);
        if (!_host.TrySave(out var error))
        {
            _host.Configuration.Document.Bindings.Add(binding);
            StatusText.Text = error ?? "A alteração não foi salva.";
            return;
        }
        _controller.Use(_host.Configuration.Document);
        _saved();
        Reload();
    }

    private void StatusToggled(object sender, RoutedEventArgs e)
    {
        var binding = FromSender(sender);
        if (binding is null)
            return;
        binding.IsEnabled = !binding.IsEnabled;
        if (!_host.TrySave(out var error))
        {
            binding.IsEnabled = !binding.IsEnabled;
            StatusText.Text = error ?? "A alteração não foi salva.";
            Reload();
            return;
        }
        _controller.Use(_host.Configuration.Document);
        _saved();
        Reload();
    }

    private void TestRowClicked(object sender, RoutedEventArgs e)
    {
        var binding = FromSender(sender);
        if (binding is null)
            return;
        StatusText.Text = "A ação sai em 2 segundos. Clique na janela que deve recebê-la.";
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            StatusText.Text = _controller.Test(binding) ?? "Ação de teste enviada.";
        };
        timer.Start();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && !_refreshingFilter)
            Reload();
    }

    private void Edit(BindingDocument? binding)
    {
        var editor = new BindingEditorWindow(_host.Configuration.Document, _session, _window, _session.Devices, binding)
        {
            Owner = Window.GetWindow(this),
        };
        if (editor.ShowDialog() == true && editor.Result is not null)
            Commit(editor.Result);
    }

    private void Commit(BindingDocument binding)
    {
        Stamp(binding);
        var list = _host.Configuration.Document.Bindings;
        var index = list.FindIndex(item => item.Id == binding.Id);
        BindingDocument? previous = null;
        if (index >= 0)
        {
            previous = list[index];
            list[index] = binding;
        }
        else
        {
            list.Add(binding);
        }

        var error = ConfigurationValidator.Validate(_host.Configuration.Document);
        if (error is not null || !_host.TrySave(out error))
        {
            if (previous is null)
                list.RemoveAt(list.Count - 1);
            else
                list[index] = previous;
            StatusText.Text = error ?? "A alteração não foi salva.";
            return;
        }
        _controller.Use(_host.Configuration.Document);
        var profile = _host.Configuration.Document.Profiles.FirstOrDefault(item => item.Id == binding.ProfileId)?.Name ?? binding.ProfileId;
        ActivityFeed.Current.Add("\uE70F", $"Binding {binding.Input} salvo no perfil {profile}");
        _saved();
        Reload();
    }

    private BindingDocument? Selected()
    {
        var row = BindingList.SelectedItem as BindingRow;
        return row is null
            ? null
            : _host.Configuration.Document.Bindings.FirstOrDefault(item => item.Id == row.Id);
    }

    private BindingDocument? FromSender(object sender)
    {
        var id = sender is FrameworkElement element ? element.Tag as string : null;
        return id is null
            ? null
            : _host.Configuration.Document.Bindings.FirstOrDefault(item => item.Id == id);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static BindingDocument Clone(BindingDocument source) => new()
    {
        Id = source.Id,
        ProfileId = source.ProfileId,
        DeviceId = source.DeviceId,
        DeviceLabel = source.DeviceLabel,
        InputKind = source.InputKind,
        InputCode = source.InputCode,
        ActionKind = source.ActionKind,
        OutputCode = source.OutputCode,
        Chord = [.. source.Chord],
        Text = source.Text,
        MacroId = source.MacroId,
        IsEnabled = source.IsEnabled,
    };

    private static void Stamp(BindingDocument binding)
    {
        binding.Input = binding.InputKind == BindingKinds.Mouse
            ? InputNames.MouseButton(binding.InputCode)
            : InputNames.Key((ushort)binding.InputCode);
        binding.Action = Describe(binding);
        if (string.IsNullOrEmpty(binding.DeviceId))
            binding.DeviceLabel = "Qualquer dispositivo";
    }

    private static string Describe(BindingDocument binding) => binding.ActionKind switch
    {
        BindingKinds.Key => InputNames.Key((ushort)binding.OutputCode),
        BindingKinds.Mouse => InputNames.MouseButton(binding.OutputCode),
        BindingKinds.Chord => string.Join(" + ", binding.Chord.Select(key => InputNames.Key((ushort)key))),
        BindingKinds.Text => "Texto",
        BindingKinds.Suppress => "Desativar entrada",
        BindingKinds.Macro => "Macro",
        _ => "Nenhuma ação",
    };

    private static string DescribeFull(ConfigurationDocument document, BindingDocument binding)
    {
        var detail = Describe(binding);
        if (binding.ActionKind == BindingKinds.Macro)
            detail = document.Macros.FirstOrDefault(macro => macro.Id == binding.MacroId)?.Name ?? detail;
        var kind = binding.ActionKind switch
        {
            BindingKinds.Key => "Tecla",
            BindingKinds.Mouse => "Botão",
            BindingKinds.Chord => "Atalho",
            BindingKinds.Text => "Texto",
            BindingKinds.Suppress => "Desativar",
            BindingKinds.Macro => "Macro",
            _ => "Ação",
        };
        return $"{kind} · {detail}";
    }

    private sealed class BindingRow
    {
        public BindingRow(string id, string device, string deviceGlyph, string input, string inputGlyph, string action, string profile, bool isEnabled)
        {
            Id = id;
            Device = device;
            DeviceGlyph = deviceGlyph;
            Input = input;
            InputGlyph = inputGlyph;
            Action = action;
            Profile = profile;
            IsEnabled = isEnabled;
            EnabledVisibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        public string Id { get; }
        public string Device { get; }
        public string DeviceGlyph { get; }
        public string Input { get; }
        public string InputGlyph { get; }
        public string Action { get; }
        public string Profile { get; }
        public bool IsEnabled { get; }
        public Visibility EnabledVisibility { get; }
    }
}
