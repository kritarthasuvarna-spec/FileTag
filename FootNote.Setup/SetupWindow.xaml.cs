using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FootNote.Setup;

/// <summary>Welcome → Install location → Options → Progress → Finish.</summary>
public partial class SetupWindow : Window
{
    private readonly SetupViewModel _vm = new();
    private int _screen; // 0..4
    private bool _succeeded;

    public SetupWindow()
    {
        InitializeComponent();
        DirBox.Text = _vm.InstallDir;
        _vm.PropertyChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            Progress.Value = _vm.ProgressValue;
            StepText.Text = _vm.CurrentStep;
        });

        if (_vm.DetectExistingInstall())
        {
            ExistingTitle.Text = $"FootNote v{_vm.ExistingVersion} is already installed";
            ExistingText.Text = $"Found at {_vm.ExistingLocation}.";
            UpdateBtn.Content = _vm.IsUpgrade
                ? $"Update to v{_vm.NewVersion}  (recommended)"
                : $"Reinstall v{_vm.NewVersion} over it";
            ShowScreen(-1);
        }
        else
        {
            ShowScreen(0);
        }
    }

    /// <summary>Update never picks a folder: it targets the registry-recorded
    /// install location and goes straight to Progress.</summary>
    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        _vm.ConfigureAsUpdate();
        ShowScreen(3);
        _succeeded = await _vm.RunAsync();
        if (!_succeeded)
        {
            FinishTitle.Text = "Update failed";
            FinishText.Text = $"{_vm.FailureMessage}\n\nDetails: {FootNote.Core.Logger.InstallLogPath}";
        }
        else
        {
            FinishTitle.Text = "FootNote has been updated";
            ShowUpdateNotes();
        }
        ShowScreen(4);
    }

    /// <summary>"What's new since you last used FootNote" — only entries between
    /// the old version (exclusive) and this build (inclusive).</summary>
    private void ShowUpdateNotes()
    {
        Version.TryParse(_vm.ExistingVersion, out var oldV);
        var entries = FootNote.App.PatchNotes.Load(
            System.Reflection.Assembly.GetExecutingAssembly(), oldV);
        if (entries.Count == 0) return;
        FinishGif.Visibility = Visibility.Collapsed; // notes take priority over the demo
        FinishNotes.Children.Add(new TextBlock
        {
            Text = "What's new since you last used FootNote:",
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            Foreground = System.Windows.Media.Brushes.White,
        });
        foreach (var (v, notes) in entries)
            foreach (string n in notes)
                FinishNotes.Children.Add(new TextBlock
                {
                    Text = $"•  {n}  (v{v.ToString(3)})",
                    TextWrapping = TextWrapping.Wrap, FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x9B, 0x9B, 0xA8)),
                    Margin = new Thickness(6, 2, 0, 0),
                });
    }

    /// <summary>Uninstall only — deliberately does NOT chain into a fresh
    /// install. Two destructive-adjacent actions stay two deliberate steps.</summary>
    private void UninstallFirst_Click(object sender, RoutedEventArgs e)
    {
        string uninst = Path.Combine(_vm.ExistingLocation!, "Uninstall.exe");
        if (File.Exists(uninst))
        {
            try { System.Diagnostics.Process.Start(uninst); } catch { }
        }
        Close(); // Setup exits; re-run it separately for a fresh install
    }

    private void ShowScreen(int n)
    {
        _screen = n;
        foreach (var (panel, idx) in new (UIElement, int)[]
                 { (ExistingPanel, -1), (WelcomePanel, 0), (LocationPanel, 1), (OptionsPanel, 2), (ProgressPanel, 3), (FinishPanel, 4) })
            panel.Visibility = idx == n ? Visibility.Visible : Visibility.Collapsed;

        StepCounter.Text = n is >= 0 and <= 2 ? $"Step {n + 1} of 3" : "";
        BackBtn.Visibility = n is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        CancelBtn.Visibility = n is >= -1 and <= 2 ? Visibility.Visible : Visibility.Collapsed;
        NextBtn.Visibility = n is 3 or -1 ? Visibility.Collapsed : Visibility.Visible;
        NextBtn.Content = n switch { 2 => "Install", 4 => "Finish", _ => "Next" };
        OpenSettingsBtn.Visibility = n == 4 && _succeeded ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        switch (_screen)
        {
            case 0:
                ShowScreen(1);
                break;

            case 1:
                string dir = DirBox.Text.Trim();
                if (dir.Length == 0) return;
                _vm.InstallDir = Path.GetFullPath(dir);
                ShowScreen(2);
                break;

            case 2:
                _vm.LaunchOnStartup = StartupCheck.IsChecked == true;
                _vm.LaunchAfterInstall = LaunchCheck.IsChecked == true;
                ShowScreen(3);
                _succeeded = await _vm.RunAsync();
                if (!_succeeded)
                {
                    FinishTitle.Text = "Setup failed";
                    FinishText.Text = $"{_vm.FailureMessage}\n\nDetails: {FootNote.Core.Logger.InstallLogPath}";
                }
                ShowScreen(4);
                break;

            case 4:
                if (_succeeded && _vm.LaunchAfterInstall) _vm.LaunchApp(openSettings: false);
                Close();
                break;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowScreen(_screen - 1);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        _vm.LaunchAfterInstall = false; // don't double-launch on Finish
        _vm.LaunchApp(openSettings: true);
        Close();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose install folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        if (dlg.ShowDialog() == true)
            DirBox.Text = Path.Combine(dlg.FolderName, "FootNote");
    }
}
