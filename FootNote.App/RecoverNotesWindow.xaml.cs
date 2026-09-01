using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FootNote.Core;
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;

namespace FootNote.App;

/// <summary>
/// Lists backed-up notes that no longer have a live comment (deleted, or lost
/// when the file moved somewhere its storage backend couldn't follow) and lets
/// the user restore any of them. A row is shown only when the file still
/// exists and genuinely has nothing live right now — nothing to fix otherwise.
/// </summary>
public partial class RecoverNotesWindow : Window
{
    /// <summary>Called after a successful restore so the app can re-index the path.</summary>
    private readonly Action<string> _onRestored;

    public RecoverNotesWindow(Action<string> onRestored)
    {
        InitializeComponent();
        _onRestored = onRestored;
        Populate();
    }

    private void Populate()
    {
        RowsPanel.Children.Clear();
        var dim = new SolidColorBrush(Color.FromRgb(0x9B, 0x9B, 0xA8));

        var candidates = NotesBackup.LoadAll()
            .Where(e => e.History.Latest is not null)
            .Where(e => File.Exists(e.Path) || Directory.Exists(e.Path))
            .Where(e => !StorageRouter.HasComment(e.Path)) // only genuinely recoverable ones
            .ToList();

        EmptyText.Visibility = candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var entry in candidates)
        {
            var latest = entry.History.Latest!;
            var card = new Border { Style = (Style)FindResource("Card") };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(entry.Path),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13.5,
            });
            stack.Children.Add(new TextBlock
            {
                Text = entry.Path,
                FontSize = 11,
                Foreground = dim,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 6),
            });
            stack.Children.Add(new TextBlock
            {
                Text = latest.Text,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
            });

            string when = entry.DeletedAtUtc is { } d
                ? $"Deleted {d.ToLocalTime():d MMM yyyy, HH:mm}"
                : $"Last saved {latest.At.ToLocalTime():d MMM yyyy, HH:mm} — no longer on the file";

            var row = new DockPanel();
            row.Children.Add(new TextBlock
            {
                Text = when, FontSize = 11, Foreground = dim, VerticalAlignment = VerticalAlignment.Center,
            });
            var restoreBtn = new Button { Content = "Restore", Style = (Style)FindResource("RestoreBtn") };
            DockPanel.SetDock(restoreBtn, Dock.Right);
            restoreBtn.Click += (_, _) => Restore(entry.Path, latest.Text, card, restoreBtn);
            row.Children.Add(restoreBtn);
            stack.Children.Add(row);

            card.Child = stack;
            RowsPanel.Children.Add(card);
        }
    }

    private void Restore(string path, string text, Border card, Button button)
    {
        try
        {
            StorageRouter.Save(path, text);
            _onRestored(path);
            button.Content = "Restored ✓";
            button.IsEnabled = false;
            card.Opacity = 0.6;
        }
        catch (Exception ex)
        {
            button.Content = "Failed";
            button.IsEnabled = false;
            System.Windows.MessageBox.Show($"Couldn't restore this note: {ex.Message}", "FootNote",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
