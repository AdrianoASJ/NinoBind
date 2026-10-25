using System.Windows;

namespace NinoBind.Views;

internal static class OverlayWindow
{
    public static void Attach(Window window)
    {
        window.Loaded += (_, _) =>
        {
            if (window.Owner is not { } owner)
                return;
            if (owner.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Maximized;
                return;
            }

            window.Left = owner.Left;
            window.Top = owner.Top;
            window.Width = owner.ActualWidth;
            window.Height = owner.ActualHeight;
        };
    }
}
