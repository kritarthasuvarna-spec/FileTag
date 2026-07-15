using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FileTag.Setup;

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
        ShowScreen(0);
    }

    private void ShowScreen(int n)
    {
        _screen = n;
        foreach (var (panel, idx) in new (UIElement, int)[]
                 { (WelcomePanel, 0), (LocationPanel, 1), (OptionsPanel, 2), (ProgressPanel, 3), (FinishPanel, 4) })
            panel.Visibility = idx == n ? Visibility.Visible : Visibility.Collapsed;

        StepCounter.Text = n <= 2 ? $"Step {n + 1} of 3" : "";
        BackBtn.Visibility = n is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        CancelBtn.Visibility = n <= 2 ? Visibility.Visible : Visibility.Collapsed;
        NextBtn.Visibility = n == 3 ? Visibility.Collapsed : Visibility.Visible;
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
                    FinishText.Text = $"{_vm.FailureMessage}\n\nDetails: {FileTag.Core.Logger.InstallLogPath}";
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
            DirBox.Text = Path.Combine(dlg.FolderName, "FileTag");
    }
}
