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

        var logArgs = $"log -1 --format=\"%H|%ci|%s\" {item.CommitHash}";
        var output = await RunGitAsync(logArgs, log);
        if (string.IsNullOrWhiteSpace(output))
        {
            // The commit is likely not in the local clone yet ("bad object").
            // Fetch it from the remote, then retry the lookup once.
            log?.Invoke($"[PR {item.PrId}] Commit {item.CommitHash} not found locally - fetching from remote...");
            if (await TryFetchCommitAsync(item.CommitHash, log))
                output = await RunGitAsync(logArgs, log);

            if (string.IsNullOrWhiteSpace(output))
            {
                log?.Invoke($"[PR {item.PrId}] git log returned no output for commit {item.CommitHash} even after fetch.");
                return null;
            }
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
    /// Fetches all refs from all remotes. Call once before processing so
    /// recently merged commits are available to `git log`.
    /// </summary>
    public async Task<bool> FetchAllAsync(Action<string>? log = null)
    {
        log?.Invoke("Running 'git fetch --all' to sync the local clone (this may take a while)...");
        var output = await RunGitAsync("fetch --all", log);
        if (string.IsNullOrWhiteSpace(output))
        {
            log?.Invoke("WARNING - git fetch --all failed. Commits may be missing from the results.");
            return false;
        }

        log?.Invoke("Fetch completed.");
        return true;
    }

    /// <summary>
    /// Fetches a missing commit from the remote. Tries a targeted fetch of the
    /// commit SHA first (fast), falling back to a full fetch of all branches.
    /// </summary>
    private async Task<bool> TryFetchCommitAsync(string commitHash, Action<string>? log)
    {
        // Fast path: fetch just the commit. Works when the server allows
        // fetching arbitrary SHAs (uploadpack.allowReachableSHA1InWant).
        var targeted = await RunGitAsync($"fetch origin {commitHash}", log);
        if (!string.IsNullOrWhiteSpace(targeted))
            return true;

        // Fallback: fetch all refs from all remotes (slower but always works).
        log?.Invoke("Targeted fetch failed - running 'git fetch --all' (this may take a while)...");
        var full = await RunGitAsync("fetch --all", log);
        return !string.IsNullOrWhiteSpace(full);
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