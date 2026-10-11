namespace TfsCherryPickHelper.Models;

public class CherryPickItem
{
    public string PrId { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public DateTime MergeDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CherryPickCommand => $"git cherry-pick {CommitHash} -m 1";

    /// <summary>
    /// Builds the TFS PR URL for cross-checking, based on the BaseUrl and RepoName
    /// used when the PR was fetched.
    /// </summary>
    public string PrUrl { get; set; } = string.Empty;
}