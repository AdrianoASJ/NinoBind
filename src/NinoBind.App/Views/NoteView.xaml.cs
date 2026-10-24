using System.Windows.Controls;

namespace NinoBind.Views;

public partial class NoteView : UserControl
{
    public NoteView(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }
}
