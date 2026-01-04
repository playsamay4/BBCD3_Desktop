using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows; // Required for MessageBox

namespace BBCD3_Desktop
{
    public class UpdateChecker
    {
        private const string RepoOwner = "playsamay4";
        private const string RepoName = "BBCD3_Desktop";
        private const string ApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        private static string IgnoredVersionFile = Path.Combine(Downloader.TEMP_DIRECTORY, "ignored_version.txt");

        public static async Task CheckForUpdatesAsync(bool isManualCheck = false)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("BBCD3-Desktop-App");

                    client.Timeout = TimeSpan.FromSeconds(5);

                    string json = await client.GetStringAsync(ApiUrl);
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                    if (release != null && !string.IsNullOrEmpty(release.TagName))
                    {
                        string cleanTag = release.TagName.TrimStart('v', 'V');

                        if (Version.TryParse(cleanTag, out Version remoteVersion))
                        {
                            Version localVersion = Assembly.GetExecutingAssembly().GetName().Version;

                            if (remoteVersion > localVersion)
                            {
                                if (!isManualCheck && IsVersionIgnored(cleanTag)) return;

                                ShowUpdateDialog(remoteVersion.ToString(), release.HtmlUrl, cleanTag, release.Body);
                            }
                            else if (isManualCheck)
                            {
                                MessageBox.Show("You are using the latest version!", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManualCheck)
                {
                    MessageBox.Show($"Could not check for updates.\nError: {ex.Message}", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private static void ShowUpdateDialog(string newVersion, string downloadUrl, string versionString, string changelog)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string msg = $"A new version (v{newVersion}) is available!";

                // Pass the changelog here
                PrettyDialog dialog = new PrettyDialog(msg, changelog);

                dialog.ShowDialog();

                if (dialog.Result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = downloadUrl,
                        UseShellExecute = true
                    });
                }
                else if (dialog.Result == MessageBoxResult.Cancel)
                {
                    IgnoreVersion(versionString);
                }
            });
        }

        private static void IgnoreVersion(string version)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IgnoredVersionFile));
                File.WriteAllText(IgnoredVersionFile, version);
            }
            catch {}
        }

        private static bool IsVersionIgnored(string version)
        {
            try
            {
                if (File.Exists(IgnoredVersionFile))
                {
                    string ignored = File.ReadAllText(IgnoredVersionFile).Trim();
                    return ignored == version;
                }
            }
            catch { }
            return false;
        }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }
    }
}