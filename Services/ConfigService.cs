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
/// The password is NOT stored in the file - it lives in the Windows Credential
/// Manager (DPAPI-encrypted, per user). A legacy plaintext password found in
/// the file is migrated to the Credential Manager and removed from the file.
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static TfsSettings Load()
    {
        var settings = new TfsSettings();

        if (File.Exists(ConfigPath))
        {
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
            }
            catch
            {
                // Corrupt or unreadable config: return defaults.
            }
        }

        settings.Password = CredentialManagerService.ReadPassword() ?? string.Empty;

        // One-time migration: move a legacy plaintext password out of the file.
        if (File.Exists(ConfigPath))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(ConfigPath));
                var legacy = node?["TfsSettings"]?["Password"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(legacy))
                {
                    if (string.IsNullOrEmpty(settings.Password))
                    {
                        CredentialManagerService.WritePassword(legacy);
                        settings.Password = legacy;
                    }

                    node!["TfsSettings"]!["Password"] = null;
                    File.WriteAllText(ConfigPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch
            {
                // Migration is best-effort; never block startup.
            }
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
                ["Username"] = settings.Username
            }
        };

        File.WriteAllText(ConfigPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // The password is stored separately in the Windows Credential Manager.
        if (string.IsNullOrEmpty(settings.Password))
            CredentialManagerService.DeletePassword();
        else
            CredentialManagerService.WritePassword(settings.Password);
    }
}