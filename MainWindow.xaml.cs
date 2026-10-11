using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TfsCherryPickHelper.Models;
using TfsCherryPickHelper.Services;

namespace TfsCherryPickHelper;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private TfsSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = ConfigService.Load();
        UpdateConnectionInfo();
        UpdateCredentialStatus();
        LocalRepoPathBox.Text = string.IsNullOrWhiteSpace(_settings.LocalRepoPath)
            ? "(not set - using application folder)"
            : _settings.LocalRepoPath;
        OutputDirBox.Text = string.IsNullOrWhiteSpace(_settings.OutputDir)
            ? "(default - project\\output)"
            : _settings.OutputDir;

        Log("Getting started:");
        Log("  1. Paste PR URLs into 'PR Links' (Base URL & repo are detected automatically).");
        Log("  2. Click 'Browse...' and select your local clone of the repository.");
        Log("  3. Click 'Login...' if not logged in, then 'Test Connection'.");
        Log("  4. Enter a CR Name and click 'Process & Generate Excel'.");
    }

    /// <summary>
    /// Updates the credential status in the header account section (top-right):
    /// green when credentials are saved. Long usernames/emails are trimmed
    /// with an ellipsis; the full name is available via tooltip.
    /// </summary>
    private void UpdateCredentialStatus()
    {
        _settings = ConfigService.Load();
        var loggedIn = _settings.HasCredentials;

        var gray = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9CA3AF"));
        var grayText = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280"));

        HeaderCredentialDot.Fill = loggedIn ? System.Windows.Media.Brushes.Green : gray;
        HeaderCredentialText.Text = loggedIn ? _settings.Username : "Not logged in";
        HeaderCredentialText.Foreground = loggedIn ? System.Windows.Media.Brushes.Green : grayText;
    }

    /// <summary>
    /// Updates the connection status chip: green after a successful test.
    /// </summary>
    private void SetConnectionStatus(bool? ok)
    {
        var gray = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9CA3AF"));
        var grayText = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280"));

        ConnectionDot.Fill = ok is null ? gray : ok.Value ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.OrangeRed;
        ConnectionText.Text = ok is null ? "Not tested" : ok.Value ? "Connected" : "Failed";
        ConnectionText.Foreground = ok is null ? grayText : ok.Value ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.OrangeRed;
    }

    /// <summary>
    /// Derives BaseUrl and RepoName from the first full PR URL in the PR Links box.
    /// </summary>
    private (string BaseUrl, string RepoName)? DetectConnection()
    {
        foreach (var line in PrLinksBox.Text.Split('\n', '\r'))
        {
            var parsed = UrlParser.ExtractBaseUrlAndRepo(line.Trim());
            if (parsed is not null)
                return parsed;
        }
        return null;
    }

    private void UpdateConnectionInfo()
    {
        var connection = DetectConnection();
        ConnectionInfoBox.Text = connection is null
            ? "(auto-detected from the first PR URL)"
            : $"{connection.Value.RepoName} @ {connection.Value.BaseUrl}";

        UpdateProcessButtonState();
    }

    /// <summary>
    /// Returns the reason the Process button is disabled, or null when ready.
    /// </summary>
    private string? GetProcessBlocker()
    {
        if (DetectConnection() is null)
            return "Paste at least one full PR URL (Base URL & repo are detected from it).";

        if (string.IsNullOrWhiteSpace(_settings.LocalRepoPath) || !Directory.Exists(_settings.LocalRepoPath))
            return "Set a valid Local Repository Path (click 'Browse...' in Settings).";

        if (string.IsNullOrWhiteSpace(CrNameBox.Text))
            return "Enter a CR Name.";

        var hasPr = PrLinksBox.Text
            .Split('\n', '\r')
            .Any(l => UrlParser.ExtractPrId(l) != string.Empty);

        if (!hasPr)
            return "Add at least one valid PR link.";

        return null;
    }

    /// <summary>
    /// Enables the Process button only when all inputs are valid,
    /// and shows what is missing via tooltip + status hint.
    /// </summary>
    private void UpdateProcessButtonState()
    {
        var blocker = GetProcessBlocker();
        ProcessButton.IsEnabled = blocker is null;
        ProcessButton.ToolTip = blocker is null
            ? "Fetch PR details and generate the Excel file"
            : blocker;

        StatusText.Text = blocker is null ? "Ready to process." : blocker;
    }

    private void Inputs_Changed(object sender, RoutedEventArgs e)
    {
        UpdateConnectionInfo();
        UpdateProcessButtonState();
    }

    private void Log(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        });
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
        StatusText.Text = "Log cleared.";
    }

    private void BrowseRepoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select local repository folder"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _settings.LocalRepoPath = dialog.FolderName;
        ConfigService.Save(_settings);
        LocalRepoPathBox.Text = dialog.FolderName;
        Log($"Local repository path set to: {dialog.FolderName}");
        UpdateProcessButtonState();
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select output folder for Excel files"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _settings.OutputDir = dialog.FolderName;
        ConfigService.Save(_settings);
        OutputDirBox.Text = dialog.FolderName;
        Log($"Output directory set to: {dialog.FolderName}");
        UpdateProcessButtonState();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var login = new LoginWindow();
        login.Owner = this;
        if (login.ShowDialog() == true)
        {
            UpdateCredentialStatus();
            Log("Credentials updated.");
        }
    }

    /// <summary>
    /// Ensures credentials exist, prompting the LoginWindow on demand.
    /// Returns false if the user cancelled the login.
    /// </summary>
    private bool EnsureCredentials()
    {
        _settings = ConfigService.Load();
        if (_settings.HasCredentials)
            return true;

        Log("Credentials not set - opening login window...");
        var login = new LoginWindow { Owner = this };
        if (login.ShowDialog() != true)
        {
            Log("Login cancelled.");
            return false;
        }

        _settings = ConfigService.Load();
        Log("Credentials saved.");
        return true;
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var connection = DetectConnection();
        if (connection is null)
        {
            MessageBox.Show(this,
                "Paste a full PR URL into the PR Links box first.\nThe TFS connection is detected from it.",
                "Test Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!EnsureCredentials())
            return;

        TestConnectionButton.IsEnabled = false;
        StatusText.Text = "Testing connection...";

        try
        {
            _settings.BaseUrl = connection.Value.BaseUrl;
            _settings.RepoName = connection.Value.RepoName;
            await RunConnectionTestAsync();
        }
        catch (Exception ex)
        {
            Log($"CONNECTION FAILED - {ex.Message}");
            StatusText.Text = "Connection failed.";
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
            SetConnectionStatus(StatusText.Text == "Connection OK.");
        }
    }

    /// <summary>
    /// Runs the connection test, re-prompting login (with auto re-test) on 401.
    /// </summary>
    private async Task<bool> RunConnectionTestWithRetryAsync()
    {
        SetConnectionStatus(null);
        await RunConnectionTestAsync();

        // Auto re-test after a fresh login.
        while (StatusText.Text == "Connection failed.")
        {
            var login = new LoginWindow { Owner = this };
            if (login.ShowDialog() != true)
            {
                SetConnectionStatus(false);
                return false;
            }

            UpdateCredentialStatus();
            Log("Credentials updated - re-testing connection...");
            await RunConnectionTestAsync();

            if (StatusText.Text == "Connection OK.")
            {
                SetConnectionStatus(true);
                return true;
            }
        }

        SetConnectionStatus(StatusText.Text == "Connection OK.");
        return StatusText.Text == "Connection OK.";
    }

    private async Task RunConnectionTestAsync()
    {
        var tfsService = new TfsApiService(_settings);
        var (success, message) = await tfsService.TestConnectionAsync(Log);
        Log(success ? $"CONNECTION OK - {message}" : $"CONNECTION FAILED - {message}");
        StatusText.Text = success ? "Connection OK." : "Connection failed.";
    }

    private async void ProcessButton_Click(object sender, RoutedEventArgs e)
    {
        // Live validation already gates the button; re-check defensively.
        var blocker = GetProcessBlocker();
        if (blocker is not null)
        {
            MessageBox.Show(this, blocker, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var crName = CrNameBox.Text.Trim();
        var prIds = PrLinksBox.Text
            .Split('\n', '\r')
            .Select(l => UrlParser.ExtractPrId(l))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        ProcessButton.IsEnabled = false;
        StatusText.Text = "Checking connection...";

        try
        {
            // Verify connectivity (with login prompt + auto re-test) before processing.
            if (!await RunConnectionTestWithRetryAsync())
            {
                StatusText.Text = "Aborted - connection failed.";
                return;
            }

            StatusText.Text = "Processing...";
            await ProcessAsync(prIds, crName);
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex.Message}");
            StatusText.Text = "Failed.";
        }
        finally
        {
            ProcessButton.IsEnabled = true;
            UpdateProcessButtonState();
        }
    }

    private async Task ProcessAsync(List<string> prIds, string crName)
    {
        Log($"Starting process for CR '{crName}' with {prIds.Count} PR(s).");

        // Derive BaseUrl/Repo from the first full PR URL and persist them.
        var connection = DetectConnection();
        if (connection is null)
        {
            Log("ERROR - no full PR URL found to detect the TFS connection.");
            StatusText.Text = "Failed - no PR URL.";
            return;
        }

        _settings.BaseUrl = connection.Value.BaseUrl;
        _settings.RepoName = connection.Value.RepoName;
        ConfigService.Save(_settings);
        Log($"TFS connection: {connection.Value.RepoName} @ {connection.Value.BaseUrl}");

        var settings = _settings;
        var tfsService = new TfsApiService(settings);
        var gitService = new GitCliService();

        // Sync the local clone first so recently merged commits are available.
        await gitService.FetchAllAsync(Log);

        var items = new List<CherryPickItem>();
        var total = prIds.Count;

        for (var index = 0; index < total; index++)
        {
            var prId = prIds[index];
            var progress = $"Processing {index + 1}/{total} - PR {prId}...";
            StatusText.Text = progress;
            Log(progress);
            try
            {
                var item = await tfsService.GetPullRequestMergeCommitAsync(prId, Log);
                if (item is null)
                {
                    Log($"[PR {prId}] SKIPPED - could not fetch PR details.");
                    continue;
                }

                item = await gitService.GetCommitDetailsAsync(item, Log);
                if (item is null)
                {
                    Log($"[PR {prId}] SKIPPED - could not fetch commit details.");
                    continue;
                }

                items.Add(item);
                Log($"[PR {prId}] OK - {item.CommitHash} @ {item.MergeDate:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Log($"[PR {prId}] ERROR - {ex.Message}");
            }
        }

        if (items.Count == 0)
        {
            Log("No PRs processed successfully. Excel file not generated.");
            StatusText.Text = "Failed - no data.";
            return;
        }

        StatusText.Text = "Generating Excel...";
        var outputPath = ExcelExporter.Export(items, crName, settings.OutputDir);
        Log($"Excel generated: {outputPath}");
        Log($"Total items: {items.Count} (sorted oldest merge first).");
        StatusText.Text = $"Done - {items.Count}/{total} PR(s) - {Path.GetFileName(outputPath)}";
    }
}