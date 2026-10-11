using System.Text.RegularExpressions;

namespace TfsCherryPickHelper.Services;

public static class UrlParser
{
    private static readonly Regex PullRequestRegex = new(@"/pullrequest/(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the PR id from a TFS/Azure DevOps pull request URL.
    /// Falls back to treating the input itself as a direct PR id.
    /// </summary>
    public static string ExtractPrId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var trimmed = input.Trim();

        var match = PullRequestRegex.Match(trimmed);
        if (match.Success)
            return match.Groups[1].Value;

        // Fallback: the input itself is a plain PR id.
        return Regex.IsMatch(trimmed, @"^\d+$") ? trimmed : string.Empty;
    }

    /// <summary>
    /// Parses a TFS PR URL of the form
    /// {host}/tfs/{collection}/{project}/_git/{repo}/pullrequest/{id}
    /// into the BaseUrl (up to and including the project) and the repo name.
    /// </summary>
    public static (string BaseUrl, string RepoName)? ExtractBaseUrlAndRepo(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();

        var gitMarker = trimmed.IndexOf("/_git/", StringComparison.OrdinalIgnoreCase);
        if (gitMarker < 0)
            return null;

        var baseUrl = trimmed.Substring(0, gitMarker);
        var afterGit = trimmed.Substring(gitMarker + "/_git/".Length);

        var repo = afterGit.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(repo))
            return null;

        return (baseUrl, repo);
    }
}