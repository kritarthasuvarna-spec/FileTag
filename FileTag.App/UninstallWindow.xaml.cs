using System.Diagnostics;
using System.IO;
using System.Windows;
using FileTag.Core;

namespace FileTag.App;

/// <summary>
/// Uninstall wizard: Confirm (live tagged-file count) → Progress (live
/// per-file count) → Finish (summary + log link). Uninstall is destructive,
/// so it is never silent for end users — the /S path exists only for
/// automated testing.
/// </summary>
public partial class UninstallWindow : Window
{
    private readonly UninstallRunner _runner = new();

    public UninstallWindow()
    {
        InitializeComponent();
        int n = _runner.TaggedFileCount;
        ConfirmText.Text =
            $"This will remove FileTag and delete comments from {n} tagged file{(n == 1 ? "" : "s")}. " +
            "The files themselves are not touched.\n\nThis can't be undone.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Logger.Info("uninstall cancelled at confirm screen");
        System.Windows.Application.Current.Shutdown(1);
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        ConfirmPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        Progress.Maximum = Math.Max(1, _runner.TaggedFileCount);

        await Task.Run(() => _runner.Run((i, total, path) => Dispatcher.BeginInvoke(() =>
        {
            Progress.Value = i;
            ProgressText.Text = $"Removing comment {i} of {total}…  {Path.GetFileName(path)}";
        })));

        ProgressPanel.Visibility = Visibility.Collapsed;
        FinishPanel.Visibility = Visibility.Visible;
        SummaryText.Text =
            $"Comments removed: {_runner.Stripped}" +
            (_runner.Skipped > 0 ? $"\nSkipped (file moved or deleted since tagging): {_runner.Skipped}" : "") +
            "\nStartup entry, Apps & Features entry, and local data have been removed.";
    }

    private void LogLink_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(Logger.UninstallLogPath) { UseShellExecute = true }); }
        catch { }
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        _runner.ScheduleInstallFolderDeletion();
        System.Windows.Application.Current.Shutdown(0);
    }
}
