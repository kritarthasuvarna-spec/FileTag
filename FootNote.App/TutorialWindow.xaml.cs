using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace FootNote.App;

/// <summary>
/// First-run interactive tutorial: one line of what FootNote does, then the
/// verified hotkey lesson — the status pill flips only when the hotkey
/// actually fires against a real selection. GIF demo as an alternative,
/// embedded, never fetched. Re-openable via tray → "Show tutorial".
/// </summary>
public partial class TutorialWindow : Window
{
    public TutorialWindow()
    {
        InitializeComponent();
        HotkeyChip.Text = Settings.SettingsService.Instance.Current.HotkeyDisplay.Replace("+", " + ");
    }

    /// <summary>Called by the app when the hotkey genuinely opened the editor on a real item.</summary>
    public void HotkeyVerified()
    {
        StatusPill.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x4D, 0x35));
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xD1, 0x8F));
        StatusText.Text = "✓ Nice — you just tagged your first file.";
        SkipBtn.Content = "Done";
    }

    private void DemoLink_Click(object sender, RoutedEventArgs e) =>
        DemoPanel.Visibility = DemoPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;

    private void Skip_Click(object sender, RoutedEventArgs e) => Close();
}
