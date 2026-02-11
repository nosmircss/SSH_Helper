using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using SSH_Helper.Services;
using SSH_Helper.UI;

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
        private readonly bool _darkMode;
        private readonly Func<bool>? _confirmExitBeforeInstall;

        private readonly Label _lblTitle;
        private readonly Label _lblVersionInfo;
        private readonly Label _lblReleaseNotesHeader;
        private readonly RichTextBox _rtbReleaseNotes;
        private readonly Panel _rtbBorderPanel;
        private readonly Label _lblQuestion;
        private readonly Button _btnYes;
        private readonly Button _btnNo;
        private readonly Button _btnSkip;
        private readonly LinkLabel _lnkViewOnGitHub;
        private readonly ProgressBar _progressBar;
        private readonly Label _lblProgress;

        private CancellationTokenSource? _downloadCts;

        public UpdateDialog(
            UpdateCheckResult updateResult,
            UpdateService updateService,
            Action<string?> onSkipVersion,
            bool enableUpdateLog = false,
            bool darkMode = false,
            Func<bool>? confirmExitBeforeInstall = null)
        {
            _updateResult = updateResult;
            _updateService = updateService;
            _onSkipVersion = onSkipVersion;
            _enableUpdateLog = enableUpdateLog;
            _darkMode = darkMode;
            _confirmExitBeforeInstall = confirmExitBeforeInstall;

            Text = "Update Available";
            Size = new Size(680, 550);
            MinimumSize = new Size(520, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _lblTitle = new Label
            {
                Text = "A new version of SSH Helper is available!",
                Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
                Location = new Point(20, 18),
                Size = new Size(620, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            _lblVersionInfo = new Label
            {
                Text = $"Installed version:  {updateResult.CurrentVersion}\n" +
                       $"Latest version:      {updateResult.LatestVersion}",
                Font = new Font("Consolas", 9.5f),
                Location = new Point(20, 50),
                Size = new Size(620, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            _lblReleaseNotesHeader = new Label
            {
                Text = "What's New:",
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Location = new Point(20, 95),
                Size = new Size(100, 20)
            };

            _rtbReleaseNotes = new RichTextBox
            {
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Segoe UI", 9f),
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DetectUrls = true
            };
            _rtbReleaseNotes.LinkClicked += RtbReleaseNotes_LinkClicked;

            _rtbBorderPanel = new Panel
            {
                Location = new Point(20, 118),
                Size = new Size(620, 270),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Padding = new Padding(1)
            };
            _rtbBorderPanel.Controls.Add(_rtbReleaseNotes);

            _lnkViewOnGitHub = new LinkLabel
            {
                Text = "View full release notes on GitHub",
                Location = new Point(20, 395),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
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
                Location = new Point(20, 422),
                Size = new Size(400, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                ForeColor = Color.FromArgb(33, 37, 41)
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(20, 448),
                Size = new Size(620, 22),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            _lblProgress = new Label
            {
                Location = new Point(20, 473),
                Size = new Size(400, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Text = "",
                Visible = false
            };

            _btnSkip = new Button
            {
                Text = "Skip This Version",
                Size = new Size(120, 34),
                Location = new Point(520, 468),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9f),
                DialogResult = DialogResult.Ignore
            };
            _btnSkip.Click += BtnSkip_Click;

            _btnNo = new Button
            {
                Text = "Not Now",
                Size = new Size(85, 34),
                Location = new Point(428, 468),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9f),
                DialogResult = DialogResult.Cancel
            };
            _btnNo.Click += BtnNo_Click;

            _btnYes = new Button
            {
                Text = "Yes, Update Now",
                Size = new Size(120, 34),
                Location = new Point(300, 468),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.None
            };
            _btnYes.FlatAppearance.BorderSize = 0;
            _btnYes.Click += BtnYes_Click;

            Controls.Add(_lblTitle);
            Controls.Add(_lblVersionInfo);
            Controls.Add(_lblReleaseNotesHeader);
            Controls.Add(_rtbBorderPanel);
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

            // Apply theme and populate release notes (RTF generation depends on theme colors)
            ApplyTheme(darkMode);
            PopulateReleaseNotes(updateResult.ReleaseNotes);
        }

        private void ApplyTheme(bool darkMode)
        {
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleButton(_btnYes, darkMode, isPrimary: true);
            DialogTheme.StyleButton(_btnNo, darkMode);
            DialogTheme.StyleButton(_btnSkip, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            if (darkMode)
            {
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }

            // RichTextBox border and background - must be set after ApplyTo which resets BorderStyle
            _rtbReleaseNotes.BorderStyle = BorderStyle.None;
            _rtbBorderPanel.BackColor = darkMode ? DialogTheme.DarkBorder : DialogTheme.LightBorder;
            _rtbReleaseNotes.BackColor = darkMode ? DialogTheme.DarkInput : Color.White;
            _rtbReleaseNotes.ForeColor = darkMode ? DialogTheme.DarkText : DialogTheme.LightText;
        }

        private void PopulateReleaseNotes(string? releaseNotes)
        {
            if (string.IsNullOrWhiteSpace(releaseNotes))
            {
                _rtbReleaseNotes.Text = "No release notes available.";
                return;
            }

            try
            {
                _rtbReleaseNotes.Rtf = FormatReleaseNotesToRtf(releaseNotes);
            }
            catch
            {
                // Fallback to plain text if RTF generation fails
                _rtbReleaseNotes.Text = FormatReleaseNotesPlainText(releaseNotes);
            }
        }

        private string FormatReleaseNotesToRtf(string markdown)
        {
            var textColor = _darkMode ? DialogTheme.DarkText : DialogTheme.LightText;
            var secondaryColor = _darkMode ? DialogTheme.DarkSecondaryText : DialogTheme.LightSecondaryText;

            var sb = new StringBuilder();

            // RTF header with color table
            sb.Append(@"{\rtf1\ansi\deff0");

            // Font table
            sb.Append(@"{\fonttbl");
            sb.Append(@"{\f0\fswiss Segoe UI;}");
            sb.Append(@"{\f1\fmodern Consolas;}");
            sb.Append('}');

            // Color table
            sb.Append(@"{\colortbl;");
            sb.Append($@"\red{textColor.R}\green{textColor.G}\blue{textColor.B};"); // \cf1 - primary text
            sb.Append($@"\red{secondaryColor.R}\green{secondaryColor.G}\blue{secondaryColor.B};"); // \cf2 - secondary text
            sb.Append(@"\red0\green120\blue212;"); // \cf3 - accent/link color
            sb.Append('}');

            // Default formatting
            sb.Append(@"\cf1\f0\fs18 "); // Segoe UI 9pt

            var lines = markdown.Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    sb.Append(@"\par ");
                    continue;
                }

                // Headers: # or ## or ###
                var headerMatch = Regex.Match(line, @"^(#{1,3})\s+(.+)$");
                if (headerMatch.Success)
                {
                    var level = headerMatch.Groups[1].Value.Length;
                    var headerText = headerMatch.Groups[2].Value;
                    var fontSize = level switch
                    {
                        1 => 28, // 14pt
                        2 => 24, // 12pt
                        _ => 22  // 11pt
                    };
                    sb.Append($@"\par\b\fs{fontSize} ");
                    AppendRtfInlineFormatted(sb, headerText);
                    sb.Append(@"\b0\fs18\par ");
                    continue;
                }

                // Bullet points: - item or * item
                var bulletMatch = Regex.Match(line, @"^[\-\*]\s+(.+)$");
                if (bulletMatch.Success)
                {
                    var bulletText = bulletMatch.Groups[1].Value;
                    sb.Append(@"\par\li360\fi-180 \bullet\~");
                    AppendRtfInlineFormatted(sb, bulletText);
                    sb.Append(@"\li0\fi0 ");
                    continue;
                }

                // Regular line
                sb.Append(@"\par ");
                AppendRtfInlineFormatted(sb, line);
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendRtfInlineFormatted(StringBuilder sb, string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                // Inline code: `code`
                if (text[i] == '`' && i + 1 < text.Length)
                {
                    var endTick = text.IndexOf('`', i + 1);
                    if (endTick > i)
                    {
                        var code = text.Substring(i + 1, endTick - i - 1);
                        sb.Append(@"{\f1 ");
                        AppendRtfEscaped(sb, code);
                        sb.Append('}');
                        i = endTick + 1;
                        continue;
                    }
                }

                // Bold: **text**
                if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
                {
                    var endBold = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (endBold > i)
                    {
                        var boldText = text.Substring(i + 2, endBold - i - 2);
                        sb.Append(@"\b ");
                        AppendRtfEscaped(sb, boldText);
                        sb.Append(@"\b0 ");
                        i = endBold + 2;
                        continue;
                    }
                }

                // Italic: *text* or _text_ (single markers)
                if ((text[i] == '*' || text[i] == '_') && i + 1 < text.Length && text[i + 1] != ' ')
                {
                    var marker = text[i];
                    // Make sure it's not ** (bold)
                    if (marker == '*' && i + 1 < text.Length && text[i + 1] == '*')
                    {
                        AppendRtfEscaped(sb, text[i]);
                        i++;
                        continue;
                    }
                    var endItalic = text.IndexOf(marker, i + 1);
                    if (endItalic > i && endItalic - i > 1)
                    {
                        var italicText = text.Substring(i + 1, endItalic - i - 1);
                        sb.Append(@"\i ");
                        AppendRtfEscaped(sb, italicText);
                        sb.Append(@"\i0 ");
                        i = endItalic + 1;
                        continue;
                    }
                }

                // Regular character
                AppendRtfEscaped(sb, text[i]);
                i++;
            }
        }

        private static void AppendRtfEscaped(StringBuilder sb, string text)
        {
            foreach (var ch in text)
            {
                AppendRtfEscaped(sb, ch);
            }
        }

        private static void AppendRtfEscaped(StringBuilder sb, char ch)
        {
            switch (ch)
            {
                case '\\': sb.Append(@"\\"); break;
                case '{': sb.Append(@"\{"); break;
                case '}': sb.Append(@"\}"); break;
                default:
                    if (ch > 127)
                        sb.Append($@"\u{(int)ch}?");
                    else
                        sb.Append(ch);
                    break;
            }
        }

        private static string FormatReleaseNotesPlainText(string releaseNotes)
        {
            var text = releaseNotes
                .Replace("\r\n", "\n")
                .Replace("\n", "\r\n")
                .Trim();

            text = Regex.Replace(text, @"^#{1,3}\s*", "", RegexOptions.Multiline);
            text = text.Replace("**", "").Replace("__", "");
            text = Regex.Replace(text, @"^\*\s+", "- ", RegexOptions.Multiline);

            return text;
        }

        private void RtbReleaseNotes_LinkClicked(object? sender, LinkClickedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.LinkText))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = e.LinkText,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void UpdateDialog_Load(object? sender, EventArgs e)
        {
            // Deselect text in release notes and set focus to Yes button
            _rtbReleaseNotes.SelectionStart = 0;
            _rtbReleaseNotes.SelectionLength = 0;
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

                _lblProgress.Text = "Verification complete.";
                _progressBar.Value = 100;

                if (_confirmExitBeforeInstall != null && !_confirmExitBeforeInstall())
                {
                    _lblProgress.Text = "Install cancelled.";
                    ResetButtons();
                    return;
                }

                _lblProgress.Text = "Installing update...";

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
    }

    /// <summary>
    /// Simple dialog shown when no updates are available (for manual check).
    /// </summary>
    internal sealed class NoUpdateDialog : Form
    {
        public NoUpdateDialog(string currentVersion, bool darkMode = false)
        {
            Text = "Check for Updates";
            Size = new Size(380, 200);
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
                Location = new Point(140, 115),
                DialogResult = DialogResult.OK
            };

            Controls.Add(lblIcon);
            Controls.Add(lblTitle);
            Controls.Add(lblMessage);
            Controls.Add(btnOk);

            AcceptButton = btnOk;

            if (darkMode)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(btnOk, true);
                // Preserve green checkmark icon color
                lblIcon.ForeColor = Color.FromArgb(40, 167, 69);
                DialogTheme.SetDarkTitleBar(this, true);
            }
        }
    }

    /// <summary>
    /// Dialog shown when update check fails.
    /// </summary>
    internal sealed class UpdateErrorDialog : Form
    {
        public UpdateErrorDialog(string errorMessage, bool darkMode = false)
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

            // Apply theme
            if (darkMode)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(btnOk, true);
                // Preserve icon colors
                lblIcon.ForeColor = Color.FromArgb(255, 193, 7);
                DialogTheme.SetDarkTitleBar(this, true);
            }
        }
    }
}
