using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TfsCherryPickHelper.Services;

public class TfsSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string LocalRepoPath { get; set; } = string.Empty;
    public string OutputDir { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// Reads and writes TfsSettings from/to appsettings.json next to the executable.
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static TfsSettings Load()
    {
        var settings = new TfsSettings();

        if (!File.Exists(ConfigPath))
            return settings;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var node = JsonNode.Parse(json);
            var tfs = node?["TfsSettings"];

            settings.BaseUrl = tfs?["BaseUrl"]?.GetValue<string>() ?? string.Empty;
            settings.RepoName = tfs?["RepoName"]?.GetValue<string>() ?? string.Empty;
            settings.LocalRepoPath = tfs?["LocalRepoPath"]?.GetValue<string>() ?? string.Empty;
            settings.OutputDir = tfs?["OutputDir"]?.GetValue<string>() ?? string.Empty;
            settings.Username = tfs?["Username"]?.GetValue<string>() ?? string.Empty;
            settings.Password = tfs?["Password"]?.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            // Corrupt or unreadable config: return defaults.
        }

        return settings;
    }

    public static void Save(TfsSettings settings)
    {
        var node = new JsonObject
        {
            ["TfsSettings"] = new JsonObject
            {
                ["BaseUrl"] = settings.BaseUrl,
                ["RepoName"] = settings.RepoName,
                ["LocalRepoPath"] = settings.LocalRepoPath,
                ["OutputDir"] = settings.OutputDir,
                ["Username"] = settings.Username,
                ["Password"] = settings.Password
            }
        };

        File.WriteAllText(ConfigPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}