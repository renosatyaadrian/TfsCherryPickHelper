using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TfsCherryPickHelper.Models;

namespace TfsCherryPickHelper.Services;

public class TfsApiService
{
    private readonly TfsSettings _settings;

    public TfsApiService(TfsSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Builds an HttpClientHandler using Negotiate (Kerberos/NTLM) authentication.
    /// The TFS server rejects Basic auth, so credentials are passed via the
    /// Negotiate handshake instead of an Authorization header.
    /// Username may be "DOMAIN\user", "DOMAIN/user", or plain "user".
    /// </summary>
    private HttpClientHandler CreateHandler()
    {
        var handler = new HttpClientHandler();

        if (_settings.HasCredentials)
        {
            var (domain, user) = SplitDomain(_settings.Username);
            handler.Credentials = new NetworkCredential(user, _settings.Password, domain);
        }
        else
        {
            handler.UseDefaultCredentials = true;
        }

        return handler;
    }

    private static (string? Domain, string User) SplitDomain(string username)
    {
        var normalized = username.Replace('/', '\\');
        var parts = normalized.Split('\\', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (null, normalized);
    }

    /// <summary>
    /// Verifies that TFS is reachable and the credentials are valid.
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync(Action<string>? log = null)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/_apis/git/repositories/{Uri.EscapeDataString(_settings.RepoName)}?api-version=5.0";

        using var handler = CreateHandler();

        if (_settings.HasCredentials)
        {
            log?.Invoke($"Testing connection using Negotiate (Kerberos/NTLM) as {_settings.Username}...");
        }
        else
        {
            log?.Invoke("Credentials empty, testing connection using default Windows credentials (NTLM)...");
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };

        try
        {
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"Connected to '{_settings.RepoName}' successfully.");
            }

            var reason = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Unauthorized - check your username/password (use 'Change Password...' to update).",
                HttpStatusCode.NotFound => $"Repository '{_settings.RepoName}' not found - check BaseUrl/RepoName in appsettings.json.",
                _ => $"HTTP {(int)response.StatusCode} {response.StatusCode}."
            };
            return (false, reason);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (false, "Connection timed out (15s). Check your network/VPN access to TFS.");
        }
    }

    /// <summary>
    /// Fetches the last merge commit of a pull request from TFS using
    /// Negotiate (Kerberos/NTLM) authentication.
    /// </summary>
    public async Task<CherryPickItem?> GetPullRequestMergeCommitAsync(string prId, Action<string>? log = null)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/_apis/git/repositories/{Uri.EscapeDataString(_settings.RepoName)}/pullrequests/{prId}?api-version=5.0";

        using var handler = CreateHandler();

        if (_settings.HasCredentials)
        {
            log?.Invoke($"[PR {prId}] Using Negotiate (Kerberos/NTLM) authentication ({_settings.Username}).");
        }
        else
        {
            log?.Invoke($"[PR {prId}] Credentials empty, using default Windows credentials (NTLM).");
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            log?.Invoke($"[PR {prId}] TFS API returned {(int)response.StatusCode} {response.StatusCode}: {content}");
            return null;
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // lastMergeCommit holds the merge commit created when the PR was completed.
        if (!root.TryGetProperty("lastMergeCommit", out var commitElement))
        {
            log?.Invoke($"[PR {prId}] No lastMergeCommit found in response (PR may not be completed yet).");
            return null;
        }

        var item = new CherryPickItem
        {
            PrId = prId,
            CommitHash = commitElement.GetProperty("commitId").GetString() ?? string.Empty,
            Message = commitElement.TryGetProperty("comment", out var comment) ? comment.GetString() ?? string.Empty : string.Empty,
            PrUrl = $"{baseUrl}/{_settings.RepoName}/_git/{_settings.RepoName}/pullrequest/{prId}"
        };

        if (commitElement.TryGetProperty("author", out var author) &&
            author.TryGetProperty("date", out var date) &&
            date.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(date.GetString(), out var parsedDate))
        {
            item.MergeDate = parsedDate;
        }

        return item;
    }
}