using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace FileTag.App;

/// <summary>"What's New" — full history from the tray menu; the Setup wizard
/// shows its own filtered slice on the update Finish screen.</summary>
public partial class PatchNotesWindow : Window
{
    public PatchNotesWindow(Version? sinceExclusive = null)
    {
        InitializeComponent();
        var dim = new SolidColorBrush(Color.FromRgb(0x9B, 0x9B, 0xA8));
        var text = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
        foreach (var (version, notes) in PatchNotes.Load(Assembly.GetExecutingAssembly(), sinceExclusive))
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = $"Version {version.ToString(3)}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14.5,
                Foreground = text,
                Margin = new Thickness(0, 12, 0, 4),
            });
            foreach (string n in notes)
            {
                NotesPanel.Children.Add(new TextBlock
                {
                    Text = "•  " + n,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12.5,
                    Foreground = dim,
                    Margin = new Thickness(8, 1, 0, 1),
                });
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
