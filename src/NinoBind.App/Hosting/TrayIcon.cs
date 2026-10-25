using System.Drawing;
using System.Windows.Forms;

namespace NinoBind.Hosting;

public sealed class TrayIcon : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/ninobind.ico");
    private readonly NotifyIcon _icon;
    private readonly Icon _image;
    private readonly ToolStripMenuItem _engineItem;

    public TrayIcon(Action open, Action toggleEngine, Action stopMacros, Action exit)
    {
        using (var stream = System.Windows.Application.GetResourceStream(IconUri).Stream)
            _image = new Icon(stream, 32, 32);
        _engineItem = new ToolStripMenuItem("Ativar motor", null, (_, _) => toggleEngine());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Abrir NinoBind", null, (_, _) => open()) { Font = new Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(_engineItem);
        menu.Items.Add(new ToolStripMenuItem("Parar macros (Ctrl + Alt + Pause)", null, (_, _) => stopMacros()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => exit()));
        _icon = new NotifyIcon
        {
            Icon = _image,
            Text = "NinoBind",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => open();
    }

    public void Update(string status, bool engineRunning, bool canToggleEngine)
    {
        var text = "NinoBind · " + status;
        _icon.Text = text.Length > 63 ? text[..63] : text;
        _engineItem.Text = engineRunning ? "Desativar motor" : "Ativar motor";
        _engineItem.Enabled = canToggleEngine;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
