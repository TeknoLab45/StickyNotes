using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StickyNotes.App;

/// <summary>Uygulama temasına uyan çerçevesiz onay diyaloğu (MessageBox yerine).</summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string confirmText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        Title = title;
    }

    /// <summary>Diyaloğu modal gösterir; onaylanırsa true.</summary>
    public static bool Ask(Window? owner, string title, string message, string confirmText)
    {
        var d = new ConfirmDialog(title, message, confirmText);
        if (owner != null && owner.IsVisible) d.Owner = owner;
        else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return d.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not Button)
        {
            try { DragMove(); } catch (System.InvalidOperationException) { }
        }
    }
}
