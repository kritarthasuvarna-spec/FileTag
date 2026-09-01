using System.Windows;

namespace FootNote.Setup;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            // /S: unattended install with defaults (used by automated tests).
            if (e.Args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase)))
            {
                var vm = new SetupViewModel { LaunchAfterInstall = false };
                if (vm.DetectExistingInstall()) vm.ConfigureAsUpdate(); // silent = update in place
                bool ok = await vm.RunAsync();
                Shutdown(ok ? 0 : 1);
                return;
            }

            new SetupWindow().Show();
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FootNote-setup-crash.log"),
                    ex.ToString());
            }
            catch { }
            MessageBox.Show("Setup hit an unexpected error:\n\n" + ex.Message, "FootNote Setup",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
