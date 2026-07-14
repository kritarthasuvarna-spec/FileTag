using System.Windows;

namespace FileTag.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var path = e.Args.Length > 0 ? e.Args[0].Trim('"') : "";
        var win = new MainWindow(path);
        win.Show();
    }
}
