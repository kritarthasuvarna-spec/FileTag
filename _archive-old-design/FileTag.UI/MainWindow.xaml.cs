using System.Windows;

namespace FileTag.UI;

public partial class MainWindow : Window
{
    public MainWindow(string filePath)
    {
        InitializeComponent();
        Sidebar.Load(filePath);
    }
}
