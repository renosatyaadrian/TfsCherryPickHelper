using System.Windows;
using TfsCherryPickHelper.Services;

namespace TfsCherryPickHelper;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // MainWindow opens directly; the login window appears on demand
        // (when Test Connection or Proses is clicked without credentials).
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }
}
