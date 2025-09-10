using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SSH_Helper
{
    internal sealed class AboutDialog : Form
    {
        private readonly Button _btnOk;
        private readonly LinkLabel _lnkHomepage;
        private readonly TextBox _txtInfo;

        public AboutDialog(string appName, string appVersion)
        {
            Text = $"About {appName}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var asm = Assembly.GetExecutingAssembly();
            string buildTime = ResolveBuildTimeSafe(asm);
            string runtime = RuntimeInformation.FrameworkDescription;

            _lnkHomepage = new LinkLabel
            {
                AutoSize = true,
                Text = "Project Home",
                Location = new Point(16, 14),
                TabStop = true
            };
            _lnkHomepage.LinkClicked += (_, __) =>
            {
                try
                {
                    var url = "https://example.com";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            _txtInfo = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.None, // we will size to content
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f),
                BackColor = Color.White,
                Location = new Point(16, _lnkHomepage.Bottom + 8),
                Width = 520
            };

            _txtInfo.Text =
$@"Application : {appName}
Version     : {appVersion}
Build Time  : {buildTime}
Author      : Chris Dudek (chris_dudek@comcast.com)
Runtime     : {runtime}

This tool executes preset CLI commands over SSH
against multiple hosts listed in the datagrid.

Use responsibly.";

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 30)
            };
            AcceptButton = _btnOk;

            Controls.Add(_lnkHomepage);
            Controls.Add(_txtInfo);
            Controls.Add(_btnOk);

            // Perform initial layout fit
            FitToContent();

            // Prevent manual resize larger/smaller than computed
            MinimumSize = Size;
            MaximumSize = new Size(Size.Width + 2, Size.Height + 2);
        }

        private void FitToContent()
        {
            int horizontalPadding = 32; // left+right padding total
            int maxLinePixelWidth = 0;
            int lineHeight = TextRenderer.MeasureText("X", _txtInfo.Font).Height;
            string[] lines = _txtInfo.Text.Replace("\r\n", "\n").Split('\n');

            using (var g = CreateGraphics())
            {
                foreach (var line in lines)
                {
                    var sz = TextRenderer.MeasureText(g, line.Length == 0 ? " " : line, _txtInfo.Font,
                                                      new Size(4000, lineHeight),
                                                      TextFormatFlags.NoPadding);
                    if (sz.Width > maxLinePixelWidth)
                        maxLinePixelWidth = sz.Width;
                }
            }

            int desiredTextWidth = Math.Min(Math.Max(360, maxLinePixelWidth + 10), 640);
            _txtInfo.Width = desiredTextWidth;

            int desiredTextHeight = (lines.Length * lineHeight) + 8; // small padding
            _txtInfo.Height = desiredTextHeight;

            // Re-center width relative to dialog
            int contentWidth = _txtInfo.Left + _txtInfo.Width + 16;
            int dialogWidth = contentWidth;
            // Position OK button
            _btnOk.Location = new Point(_txtInfo.Left + _txtInfo.Width - _btnOk.Width,
                                        _txtInfo.Bottom + 14);

            int dialogHeight = _btnOk.Bottom + 16;

            ClientSize = new Size(dialogWidth, dialogHeight);
        }

        private static string ResolveBuildTimeSafe(Assembly asm)
        {
            try
            {
                string? path = asm.Location;
                if (string.IsNullOrEmpty(path))
                    path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path))
                    path = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(path))
                    path = Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch { }
            return "Unknown";
        }
    }
}