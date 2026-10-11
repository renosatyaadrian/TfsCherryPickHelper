using System.Diagnostics;
using System.IO;
using TfsCherryPickHelper.Models;

namespace TfsCherryPickHelper.Services;

public class GitCliService
{
    /// <summary>
    /// Runs `git log -1 --format="%H|%ci|%s" {commitHash}` to fetch commit details.
    /// </summary>
    public async Task<CherryPickItem?> GetCommitDetailsAsync(CherryPickItem item, Action<string>? log = null)
    {
        var workingDir = GetWorkingDirectory();

        if (!Directory.Exists(workingDir))
        {
            log?.Invoke($"[PR {item.PrId}] ERROR - Local repository path does not exist: '{workingDir}'. " +
                        "Set it via the 'Browse...' button in Settings.");
            return null;
        }

        var output = await RunGitAsync($"log -1 --format=\"%H|%ci|%s\" {item.CommitHash}", log);
        if (string.IsNullOrWhiteSpace(output))
        {
            log?.Invoke($"[PR {item.PrId}] git log returned no output for commit {item.CommitHash}.");
            return null;
        }

        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (line is null)
            return null;

        var parts = line.Split('|', 3);
        if (parts.Length < 3)
        {
            log?.Invoke($"[PR {item.PrId}] Unexpected git log output: {line}");
            return null;
        }

        item.CommitHash = parts[0].Trim('"');
        if (DateTime.TryParse(parts[1], out var date))
            item.MergeDate = date;
        item.Message = parts[2];

        return item;
    }

    /// <summary>
    /// Executes a git command in the configured local repository path.
    /// </summary>
    public async Task<string> RunGitAsync(string arguments, Action<string>? log = null)
    {
        var workingDir = GetWorkingDirectory();

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Inject credentials as environment variables when available.
        var settings = ConfigService.Load();
        if (settings.HasCredentials)
        {
            psi.EnvironmentVariables["GIT_USERNAME"] = settings.Username;
            psi.EnvironmentVariables["GIT_PASSWORD"] = settings.Password;
        }

        log?.Invoke($"> git {arguments}");

        using var process = Process.Start(psi);
        if (process is null)
            return string.Empty;

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            var error = stderr.Trim();
            log?.Invoke($"git error: {error}");

            if (error.Contains("bad object"))
            {
                log?.Invoke("Hint: the commit is not in the local clone yet. Run 'git fetch --all' in the local repository, then try again.");
            }
        }

        return stdout;
    }

    private static string GetWorkingDirectory()
    {
        var path = ConfigService.Load().LocalRepoPath;
        return string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : path;
    }
}