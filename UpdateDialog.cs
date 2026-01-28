using System.Diagnostics;
using SSH_Helper.Services;

namespace SSH_Helper
{
    /// <summary>
    /// Dialog shown when an update is available.
    /// </summary>
    internal sealed class UpdateDialog : Form
    {
        private readonly UpdateCheckResult _updateResult;
        private readonly UpdateService _updateService;
        private readonly Action<string?> _onSkipVersion;
        private readonly bool _enableUpdateLog;

        private readonly Label _lblTitle;
        private readonly Label _lblVersionInfo;
        private readonly Label _lblQuestion;
        private readonly TextBox _txtReleaseNotes;
        private readonly Button _btnYes;
        private readonly Button _btnNo;
        private readonly Button _btnSkip;
        private readonly LinkLabel _lnkViewOnGitHub;
        private readonly ProgressBar _progressBar;
        private readonly Label _lblProgress;

        private CancellationTokenSource? _downloadCts;

        public UpdateDialog(UpdateCheckResult updateResult, UpdateService updateService, Action<string?> onSkipVersion, bool enableUpdateLog = false)
        {
            _updateResult = updateResult;
            _updateService = updateService;
            _onSkipVersion = onSkipVersion;
            _enableUpdateLog = enableUpdateLog;

            Text = "Update Available";
            Size = new Size(520, 450);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _lblTitle = new Label
            {
                Text = "A new version of SSH Helper is available!",
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                Location = new Point(20, 18),
                Size = new Size(460, 28),
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            _lblVersionInfo = new Label
            {
                Text = $"Installed version:  {updateResult.CurrentVersion}\n" +
                       $"Latest version:      {updateResult.LatestVersion}",
                Font = new Font("Consolas", 9.5f),
                Location = new Point(20, 50),
                Size = new Size(460, 38),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            var lblReleaseNotes = new Label
            {
                Text = "What's New:",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(20, 95),
                Size = new Size(100, 20)
            };

            _txtReleaseNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = FormatReleaseNotes(updateResult.ReleaseNotes),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(20, 118),
                Size = new Size(460, 170),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _lnkViewOnGitHub = new LinkLabel
            {
                Text = "View full release notes on GitHub",
                Location = new Point(20, 295),
                AutoSize = true
            };
            _lnkViewOnGitHub.LinkClicked += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_updateResult.ReleaseUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _updateResult.ReleaseUrl,
                        UseShellExecute = true
                    });
                }
            };

            _lblQuestion = new Label
            {
                Text = "Would you like to download and install this update now?",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(20, 325),
                Size = new Size(460, 20),
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(20, 350),
                Size = new Size(460, 22),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            _lblProgress = new Label
            {
                Location = new Point(20, 375),
                Size = new Size(460, 20),
                Text = "",
                Visible = false
            };

            _btnYes = new Button
            {
                Text = "Yes, Update Now",
                Size = new Size(120, 34),
                Location = new Point(130, 370),
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.None
            };
            _btnYes.FlatAppearance.BorderSize = 0;
            _btnYes.Click += BtnYes_Click;

            _btnNo = new Button
            {
                Text = "Not Now",
                Size = new Size(85, 34),
                Location = new Point(258, 370),
                Font = new Font("Segoe UI", 9f),
                DialogResult = DialogResult.Cancel
            };
            _btnNo.Click += BtnNo_Click;

            _btnSkip = new Button
            {
                Text = "Skip This Version",
                Size = new Size(120, 34),
                Location = new Point(351, 370),
                Font = new Font("Segoe UI", 9f),
                DialogResult = DialogResult.Ignore
            };
            _btnSkip.Click += BtnSkip_Click;

            Controls.Add(_lblTitle);
            Controls.Add(_lblVersionInfo);
            Controls.Add(lblReleaseNotes);
            Controls.Add(_txtReleaseNotes);
            Controls.Add(_lnkViewOnGitHub);
            Controls.Add(_lblQuestion);
            Controls.Add(_progressBar);
            Controls.Add(_lblProgress);
            Controls.Add(_btnYes);
            Controls.Add(_btnNo);
            Controls.Add(_btnSkip);

            AcceptButton = _btnYes;
            CancelButton = _btnNo;

            FormClosing += UpdateDialog_FormClosing;
            Load += UpdateDialog_Load;
        }

        private void UpdateDialog_Load(object? sender, EventArgs e)
        {
            // Deselect text in release notes and set focus to Yes button
            _txtReleaseNotes.SelectionStart = 0;
            _txtReleaseNotes.SelectionLength = 0;
            _btnYes.Focus();
        }

        private void UpdateDialog_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _downloadCts?.Cancel();
        }

        private void BtnNo_Click(object? sender, EventArgs e)
        {
            // Just close - user will be prompted again next time
            DialogResult = DialogResult.No;
            Close();
        }

        private void BtnSkip_Click(object? sender, EventArgs e)
        {
            // Skip this version so user won't be prompted again until a newer version is available
            _onSkipVersion(_updateResult.LatestVersion);
            DialogResult = DialogResult.Ignore;
            Close();
        }

        private async void BtnYes_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_updateResult.DownloadUrl))
            {
                if (!string.IsNullOrEmpty(_updateResult.ReleaseUrl))
                {
                    var result = MessageBox.Show(
                        $"Version {_updateResult.LatestVersion} is available but no direct download was found.\n\n" +
                        "Would you like to open the GitHub release page to download it manually?",
                        "Download Not Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _updateResult.ReleaseUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"Version {_updateResult.LatestVersion} is available but no download information was found.\n\n" +
                        "Please check the GitHub repository for the latest release.",
                        "Download Not Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            // Show progress UI
            _btnYes.Visible = false;
            _btnNo.Visible = false;
            _btnSkip.Visible = false;
            _lblQuestion.Visible = false;
            _progressBar.Visible = true;
            _lblProgress.Visible = true;
            _progressBar.Value = 0;
            _lblProgress.Text = "Downloading update...";

            _downloadCts = new CancellationTokenSource();

            _updateService.DownloadProgressChanged += UpdateService_DownloadProgressChanged;

            // Progress reporter for retry attempts
            var retryProgress = new Progress<DownloadRetryEventArgs>(args =>
            {
                _lblProgress.Text = $"Download failed, retrying ({args.Attempt}/{args.MaxAttempts})...";
                _progressBar.Value = 0;
            });

            try
            {
                var downloadPath = await _updateService.DownloadUpdateAsync(
                    _updateResult.DownloadUrl,
                    _downloadCts.Token,
                    maxRetries: 3,
                    retryProgress: retryProgress);

                if (string.IsNullOrWhiteSpace(_updateResult.ChecksumUrl))
                {
                    MessageBox.Show(
                        "This update does not include checksum information and cannot be verified. " +
                        "Please download the update manually from GitHub.",
                        "Verification Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    _lblProgress.Text = "Verification failed.";
                    ResetButtons();
                    return;
                }

                _lblProgress.Text = "Verifying update...";
                await _updateService.VerifyUpdatePackageAsync(downloadPath, _updateResult.ChecksumUrl, _downloadCts.Token);

                _lblProgress.Text = "Verification complete. Installing update...";
                _progressBar.Value = 100;

                // Give UI a moment to update
                await Task.Delay(500);

                // Launch updater and exit
                _updateService.LaunchUpdaterAndExit(downloadPath, null, _enableUpdateLog);
            }
            catch (OperationCanceledException)
            {
                _lblProgress.Text = "Download cancelled.";
                ResetButtons();
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(
                    $"Update verification failed: {ex.Message}",
                    "Verification Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                _lblProgress.Text = "Verification failed.";
                ResetButtons();
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(
                    $"{ex.Message}\n\nThe release page will open so you can download manually.",
                    "Updater Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                if (!string.IsNullOrEmpty(_updateResult.ReleaseUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _updateResult.ReleaseUrl,
                        UseShellExecute = true
                    });
                }
                ResetButtons();
            }
            catch (Exception ex)
            {
                var friendlyMessage = UpdateService.GetUserFriendlyErrorMessage(ex);
                var isRetryable = UpdateService.IsRetryableException(ex);

                var message = $"Failed to download update.\n\n{friendlyMessage}";
                if (isRetryable)
                {
                    message += "\n\nWould you like to try again, or download the update manually from GitHub?";
                }
                else
                {
                    message += "\n\nYou can download the update manually from GitHub.";
                }

                var buttons = isRetryable
                    ? MessageBoxButtons.YesNoCancel
                    : MessageBoxButtons.OK;

                var result = MessageBox.Show(
                    message,
                    "Download Error",
                    buttons,
                    MessageBoxIcon.Error);

                if (result == DialogResult.Yes)
                {
                    // Retry the download
                    _updateService.DownloadProgressChanged -= UpdateService_DownloadProgressChanged;
                    BtnYes_Click(sender, e);
                    return;
                }
                else if (result == DialogResult.No && !string.IsNullOrEmpty(_updateResult.ReleaseUrl))
                {
                    // Open GitHub release page for manual download
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _updateResult.ReleaseUrl,
                        UseShellExecute = true
                    });
                }

                _lblProgress.Text = "Download failed.";
                ResetButtons();
            }
            finally
            {
                _updateService.DownloadProgressChanged -= UpdateService_DownloadProgressChanged;
            }
        }

        private void ResetButtons()
        {
            _progressBar.Visible = false;
            _lblProgress.Visible = false;
            _lblQuestion.Visible = true;
            _btnYes.Visible = true;
            _btnNo.Visible = true;
            _btnSkip.Visible = true;
        }

        private void UpdateService_DownloadProgressChanged(object? sender, UpdateDownloadProgressEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateDownloadProgress(e));
            }
            else
            {
                UpdateDownloadProgress(e);
            }
        }

        private void UpdateDownloadProgress(UpdateDownloadProgressEventArgs e)
        {
            _progressBar.Value = e.ProgressPercent;

            var downloadedMb = e.BytesDownloaded / (1024.0 * 1024.0);
            var totalMb = e.TotalBytes / (1024.0 * 1024.0);
            _lblProgress.Text = $"Downloading: {downloadedMb:F1} MB / {totalMb:F1} MB ({e.ProgressPercent}%)";
        }

        private static string FormatReleaseNotes(string? releaseNotes)
        {
            if (string.IsNullOrWhiteSpace(releaseNotes))
                return "No release notes available.";

            // Basic cleanup of markdown for display in a TextBox
            var text = releaseNotes
                .Replace("\r\n", "\n")
                .Replace("\n", "\r\n")
                .Trim();

            // Remove common markdown headers (## or ###) but keep the text
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^#{1,3}\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            // Remove markdown bold/italic markers
            text = text.Replace("**", "").Replace("__", "");

            // Convert markdown bullet points to simple dashes
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\*\s+", "- ", System.Text.RegularExpressions.RegexOptions.Multiline);

            return text;
        }
    }

    /// <summary>
    /// Simple dialog shown when no updates are available (for manual check).
    /// </summary>
    internal sealed class NoUpdateDialog : Form
    {
        public NoUpdateDialog(string currentVersion)
        {
            Text = "Check for Updates";
            Size = new Size(380, 170);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lblIcon = new Label
            {
                Text = "\u2714", // Check mark
                Font = new Font("Segoe UI", 28f),
                ForeColor = Color.FromArgb(40, 167, 69),
                Location = new Point(25, 22),
                Size = new Size(50, 55)
            };

            var lblTitle = new Label
            {
                Text = "You're up to date!",
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                Location = new Point(80, 25),
                Size = new Size(270, 25)
            };

            var lblMessage = new Label
            {
                Text = $"SSH Helper {currentVersion} is the latest version.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(80, 52),
                Size = new Size(270, 25)
            };

            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(90, 32),
                Location = new Point(140, 95),
                DialogResult = DialogResult.OK
            };

            Controls.Add(lblIcon);
            Controls.Add(lblTitle);
            Controls.Add(lblMessage);
            Controls.Add(btnOk);

            AcceptButton = btnOk;
        }
    }

    /// <summary>
    /// Dialog shown when update check fails.
    /// </summary>
    internal sealed class UpdateErrorDialog : Form
    {
        public UpdateErrorDialog(string errorMessage)
        {
            Text = "Update Check Failed";
            Size = new Size(420, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lblIcon = new Label
            {
                Text = "\u26A0", // Warning
                Font = new Font("Segoe UI", 28f),
                ForeColor = Color.FromArgb(255, 193, 7),
                Location = new Point(25, 22),
                Size = new Size(50, 55)
            };

            var lblTitle = new Label
            {
                Text = "Could not check for updates",
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                Location = new Point(80, 22),
                Size = new Size(300, 25)
            };

            var lblMessage = new Label
            {
                Text = errorMessage,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(80, 50),
                Size = new Size(310, 50)
            };

            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(90, 32),
                Location = new Point(160, 110),
                DialogResult = DialogResult.OK
            };

            Controls.Add(lblIcon);
            Controls.Add(lblTitle);
            Controls.Add(lblMessage);
            Controls.Add(btnOk);

            AcceptButton = btnOk;
        }
    }
}
