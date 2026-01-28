using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Information about a GitHub release.
    /// </summary>
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();

        /// <summary>
        /// Gets the version string from the tag (strips leading 'v' if present).
        /// </summary>
        public string Version => TagName.TrimStart('v', 'V');
    }

    /// <summary>
    /// Information about a release asset (downloadable file).
    /// </summary>
    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of an update check.
    /// </summary>
    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string? ChecksumUrl { get; set; }
        public string ChecksumAlgorithm { get; set; } = "SHA256";
        public string ReleaseUrl { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public GitHubRelease? Release { get; set; }
    }

    /// <summary>
    /// Service for checking and downloading updates from GitHub releases.
    /// </summary>
    public class UpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _currentVersion;
        private bool _disposed;

        /// <summary>
        /// Event raised during download to report progress.
        /// </summary>
        public event EventHandler<UpdateDownloadProgressEventArgs>? DownloadProgressChanged;

        /// <summary>
        /// Creates a new UpdateService.
        /// </summary>
        /// <param name="owner">GitHub repository owner (username or organization)</param>
        /// <param name="repo">GitHub repository name</param>
        /// <param name="currentVersion">Current application version (e.g., "0.40-Beta")</param>
        public UpdateService(string owner, string repo, string currentVersion)
        {
            _owner = owner;
            _repo = repo;
            _currentVersion = currentVersion;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"{repo}/{currentVersion}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        /// <summary>
        /// Checks GitHub for the latest release and compares with current version.
        /// </summary>
        public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = _currentVersion
            };

            try
            {
                var apiUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
                var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        result.ErrorMessage = "No releases found for this repository.";
                    }
                    else
                    {
                        result.ErrorMessage = $"GitHub API returned {(int)response.StatusCode}: {response.ReasonPhrase}";
                    }
                    return result;
                }

                var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken);
                if (release == null)
                {
                    result.ErrorMessage = "Failed to parse release information.";
                    return result;
                }

                result.Release = release;
                result.LatestVersion = release.Version;
                result.ReleaseNotes = release.Body;
                result.ReleaseUrl = release.HtmlUrl;

                // Find the appropriate download asset (prefer .zip, then .exe)
                var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                         ?? release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                         ?? release.Assets.FirstOrDefault();

                if (asset != null)
                {
                    result.DownloadUrl = asset.DownloadUrl;
                    var checksumAsset = FindChecksumAsset(release, asset);
                    if (checksumAsset != null)
                    {
                        result.ChecksumUrl = checksumAsset.DownloadUrl;
                    }
                }

                // Compare versions
                result.UpdateAvailable = IsNewerVersion(release.Version, _currentVersion);
            }
            catch (HttpRequestException ex)
            {
                result.ErrorMessage = $"Network error: {ex.Message}";
            }
            catch (TaskCanceledException)
            {
                result.ErrorMessage = "Update check was cancelled.";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error checking for updates: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Downloads the update to a temporary location.
        /// </summary>
        /// <param name="downloadUrl">URL to download from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the downloaded file</returns>
        public async Task<string> DownloadUpdateAsync(string downloadUrl, CancellationToken cancellationToken = default)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SSH_Helper_Update");
            Directory.CreateDirectory(tempDir);

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var downloadPath = Path.Combine(tempDir, fileName);

            // Delete existing file if present
            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var buffer = new byte[81920]; // 80KB buffer

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercent = (int)((totalBytesRead * 100) / totalBytes);
                    DownloadProgressChanged?.Invoke(this, new UpdateDownloadProgressEventArgs(totalBytesRead, totalBytes, progressPercent));
                }
            }

            return downloadPath;
        }

        /// <summary>
        /// Verifies the downloaded update package using a SHA256 checksum.
        /// </summary>
        /// <param name="updatePackagePath">Path to the downloaded update package</param>
        /// <param name="checksumUrl">URL to download the checksum from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task VerifyUpdatePackageAsync(string updatePackagePath, string checksumUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(checksumUrl))
                throw new InvalidDataException("Checksum URL is missing.");

            var checksumContent = await _httpClient.GetStringAsync(checksumUrl, cancellationToken);
            var expectedHash = ExtractSha256(checksumContent, Path.GetFileName(updatePackagePath));
            if (string.IsNullOrWhiteSpace(expectedHash))
                throw new InvalidDataException("Checksum file does not contain a valid SHA256 hash.");

            var actualHash = ComputeSha256(updatePackagePath);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update package checksum verification failed.");
        }

        /// <summary>
        /// Launches the updater script and closes the current application.
        /// Uses an embedded PowerShell script to perform the update.
        /// </summary>
        /// <param name="updatePackagePath">Path to the downloaded update package</param>
        /// <param name="currentExePath">Path to the current executable (optional, auto-detected if null)</param>
        /// <param name="enableLogging">Whether to enable logging for troubleshooting</param>
        public void LaunchUpdaterAndExit(string updatePackagePath, string? currentExePath = null, bool enableLogging = false)
        {
            currentExePath ??= Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath))
                throw new InvalidOperationException("Could not determine current executable path.");

            var appDir = Path.GetDirectoryName(currentExePath)!;
            var tempDir = Path.Combine(Path.GetTempPath(), "SSH_Helper_Update");
            Directory.CreateDirectory(tempDir);

            // Create the PowerShell updater script
            var scriptPath = Path.Combine(tempDir, "update.ps1");
            File.WriteAllText(scriptPath, GetUpdaterScript());

            // Run PowerShell with the script
            // -NoProfile speeds up startup and avoids profile issues
            // -ExecutionPolicy Bypass allows running unsigned scripts
            // EnableLog is a switch parameter - only include it if logging is enabled
            var enableLogArg = enableLogging ? "-EnableLog" : "";
            var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                          $"-UpdatePackage \"{updatePackagePath}\" " +
                          $"-TargetDir \"{appDir}\" " +
                          $"-ExeToLaunch \"{currentExePath}\" " +
                          $"-ProcessId {Environment.ProcessId} " +
                          enableLogArg;

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-WindowStyle Hidden " + arguments,
                UseShellExecute = true,
                WorkingDirectory = tempDir,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            Environment.Exit(0);
        }

        /// <summary>
        /// Gets the embedded PowerShell updater script.
        /// </summary>
        private static string GetUpdaterScript()
        {
            return @"
param(
    [Parameter(Mandatory=$true)][string]$UpdatePackage,
    [Parameter(Mandatory=$true)][string]$TargetDir,
    [Parameter(Mandatory=$true)][string]$ExeToLaunch,
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [int]$ShutdownTimeoutSec = 60,
    [switch]$EnableLog
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$logFile = Join-Path $scriptDir 'update.log'
$log = [System.Collections.Generic.List[string]]::new()

function Write-Log {
    param([string]$Message, [string]$Color = 'White')
    Write-Host $Message -ForegroundColor $Color
    if ($EnableLog) { $log.Add(""[$(Get-Date -Format 'HH:mm:ss')] $Message"") }
}

function Save-Log {
    if ($EnableLog -and $log.Count -gt 0) {
        $log | Out-File -FilePath $logFile -Encoding UTF8
    }
}

function Pause-IfInteractive {
    try {
        if ([Environment]::UserInteractive -and $Host.Name -eq 'ConsoleHost') {
            $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
        }
    } catch { }
}

try {
    # Validate inputs
    if (-not (Test-Path $UpdatePackage)) {
        Write-Log ""ERROR: Update package not found: $UpdatePackage"" 'Red'
        Save-Log; Pause-IfInteractive; exit 1
    }
    if (-not (Test-Path $TargetDir)) {
        Write-Log ""ERROR: Target directory not found: $TargetDir"" 'Red'
        Save-Log; Pause-IfInteractive; exit 1
    }

    Write-Log 'Waiting for SSH Helper to close...' 'Yellow'

    # Wait for process to exit (default 60 seconds)
    if ($ShutdownTimeoutSec -lt 5) { $ShutdownTimeoutSec = 5 }
    $timeout = [DateTime]::Now.AddSeconds($ShutdownTimeoutSec)
    while ([DateTime]::Now -lt $timeout) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 200
    }

    if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
        Write-Log 'Timed out waiting for SSH Helper to close.' 'Red'
        Save-Log; Pause-IfInteractive; exit 1
    }

    # Brief pause for file handles
    Start-Sleep -Seconds 1

    Write-Log 'Installing update...' 'Yellow'

    if ($UpdatePackage -like '*.zip') {
        Expand-Archive -Path $UpdatePackage -DestinationPath $TargetDir -Force -ErrorAction Stop
    } else {
        $destPath = Join-Path $TargetDir (Split-Path $UpdatePackage -Leaf)
        Copy-Item -Path $UpdatePackage -Destination $destPath -Force -ErrorAction Stop
        if (-not (Test-Path $destPath)) {
            throw ""Update copy failed: $destPath not found.""
        }
    }

    Remove-Item -Path $UpdatePackage -Force -ErrorAction SilentlyContinue

    Write-Log 'Update complete!' 'Green'
    Save-Log

    $exePath = $ExeToLaunch
    if (-not (Test-Path $exePath)) {
        $exeName = Split-Path $ExeToLaunch -Leaf
        $fallback = Join-Path $TargetDir $exeName
        if (Test-Path $fallback) {
            $exePath = $fallback
        } else {
            throw ""Executable not found after update: $ExeToLaunch""
        }
    }
    Start-Process -FilePath $exePath -WorkingDirectory $TargetDir -ErrorAction Stop

    # Clean up temp directory (delayed)
    if (-not $EnableLog) {
        Start-Process -FilePath 'cmd.exe' -ArgumentList ""/c timeout /t 2 /nobreak >nul & rd /s /q `""$scriptDir`"""" -WindowStyle Hidden
    }
    exit 0

} catch {
    Write-Log ""Update failed: $_"" 'Red'
    Write-Log ""$($_.ScriptStackTrace)"" 'Red'
    Save-Log
    Pause-IfInteractive
    exit 1
}
";
        }

        private static GitHubAsset? FindChecksumAsset(GitHubRelease release, GitHubAsset targetAsset)
        {
            var suffixes = new[]
            {
                ".sha256",
                ".sha256.txt",
                ".sha256sum",
                ".sha256sum.txt"
            };

            foreach (var suffix in suffixes)
            {
                var expectedName = targetAsset.Name + suffix;
                var match = release.Assets.FirstOrDefault(a =>
                    string.Equals(a.Name, expectedName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            return null;
        }

        private static string? ExtractSha256(string content, string? fileName)
        {
            string? fallback = null;
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (!IsHexHash(part))
                        continue;

                    if (!string.IsNullOrEmpty(fileName) &&
                        trimmed.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return part;
                    }

                    fallback ??= part;
                    break;
                }
            }

            return fallback;
        }

        private static bool IsHexHash(string value)
        {
            if (value.Length != 64)
                return false;

            foreach (var c in value)
            {
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }

            return true;
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Compares two version strings to determine if the new version is newer.
        /// Handles formats like "0.40-Beta", "1.0.0", "v1.2.3", etc.
        /// </summary>
        public static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            // Normalize versions: remove 'v' prefix, replace common separators
            static string Normalize(string v) => v.TrimStart('v', 'V').Trim();

            var newNorm = Normalize(newVersion);
            var currentNorm = Normalize(currentVersion);

            // Try semantic version comparison first
            if (TryParseSemanticVersion(newNorm, out var newParts, out var newPrerelease) &&
                TryParseSemanticVersion(currentNorm, out var currentParts, out var currentPrerelease))
            {
                // Compare numeric parts
                for (int i = 0; i < Math.Max(newParts.Length, currentParts.Length); i++)
                {
                    int newPart = i < newParts.Length ? newParts[i] : 0;
                    int currentPart = i < currentParts.Length ? currentParts[i] : 0;

                    if (newPart > currentPart) return true;
                    if (newPart < currentPart) return false;
                }

                // Numeric parts are equal, compare pre-release
                // A version without pre-release is newer than one with pre-release
                if (string.IsNullOrEmpty(newPrerelease) && !string.IsNullOrEmpty(currentPrerelease))
                    return true;
                if (!string.IsNullOrEmpty(newPrerelease) && string.IsNullOrEmpty(currentPrerelease))
                    return false;

                // Both have pre-release, compare alphabetically (Beta > Alpha, RC > Beta, etc.)
                return string.Compare(newPrerelease, currentPrerelease, StringComparison.OrdinalIgnoreCase) > 0;
            }

            // Fallback to string comparison
            return string.Compare(newNorm, currentNorm, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static bool TryParseSemanticVersion(string version, out int[] parts, out string prerelease)
        {
            parts = Array.Empty<int>();
            prerelease = string.Empty;

            // Split on hyphen to separate version from pre-release
            var hyphenIndex = version.IndexOf('-');
            string versionPart;

            if (hyphenIndex >= 0)
            {
                versionPart = version.Substring(0, hyphenIndex);
                prerelease = version.Substring(hyphenIndex + 1);
            }
            else
            {
                versionPart = version;
            }

            // Parse numeric parts
            var segments = versionPart.Split('.');
            var parsedParts = new List<int>();

            foreach (var segment in segments)
            {
                // Extract leading digits
                var digits = new string(segment.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var num))
                {
                    parsedParts.Add(num);
                }
                else if (parsedParts.Count == 0)
                {
                    return false; // First segment must have a number
                }
            }

            parts = parsedParts.ToArray();
            return parts.Length > 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Event arguments for download progress updates.
    /// </summary>
    public class UpdateDownloadProgressEventArgs : EventArgs
    {
        public long BytesDownloaded { get; }
        public long TotalBytes { get; }
        public int ProgressPercent { get; }

        public UpdateDownloadProgressEventArgs(long bytesDownloaded, long totalBytes, int progressPercent)
        {
            BytesDownloaded = bytesDownloaded;
            TotalBytes = totalBytes;
            ProgressPercent = progressPercent;
        }
    }
}
