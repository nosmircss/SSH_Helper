using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Transfers files over SFTP using SSH.NET.
    /// </summary>
    public class SftpCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Sftp == null)
                return Task.FromResult(CommandResult.Fail("Sftp command has no options"));

            var options = step.Sftp;
            var into = options.Into;
            CaptureFailure(into, context);

            var action = context.SubstituteVariables(options.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (action != "upload" && action != "download")
                return Task.FromResult(ApplyOnError(step, "Sftp requires 'action' to be upload or download"));

            var localPath = ResolveLocalPath(context, options.LocalPath);
            var remotePath = context.SubstituteVariables(options.RemotePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(localPath))
                return Task.FromResult(ApplyOnError(step, "Sftp requires 'local_path'"));
            if (string.IsNullOrWhiteSpace(remotePath))
                return Task.FromResult(ApplyOnError(step, "Sftp requires 'remote_path'"));

            var timeoutSeconds = options.Timeout > 0 ? options.Timeout : 120;

            if (action == "download")
            {
                if (!options.Overwrite && File.Exists(localPath))
                {
                    return Task.FromResult(ApplyOnError(step, $"Sftp download destination already exists: {localPath}"));
                }

                var localDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrWhiteSpace(localDir))
                    Directory.CreateDirectory(localDir);
            }
            else
            {
                if (!File.Exists(localPath))
                    return Task.FromResult(ApplyOnError(step, $"Sftp upload source not found: {localPath}"));
            }

            ResolveEndpoint(options, context, out var host, out var port, out var username, out var password);
            if (string.IsNullOrWhiteSpace(host))
                return Task.FromResult(ApplyOnError(step, "Sftp requires a host (or Host_IP in current context)"));
            if (string.IsNullOrWhiteSpace(username))
                return Task.FromResult(ApplyOnError(step, "Sftp requires username (or username in current context)"));
            if (string.IsNullOrWhiteSpace(password))
                return Task.FromResult(ApplyOnError(step, "Sftp requires password (or password in current context)"));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var sftp = new SftpClient(host, port, username, password)
                {
                    OperationTimeout = TimeSpan.FromSeconds(timeoutSeconds)
                };

                sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                sftp.Connect();

                if (action == "upload" && !options.Overwrite && sftp.Exists(remotePath))
                {
                    return Task.FromResult(ApplyOnError(step, $"Sftp upload destination already exists: {remotePath}"));
                }

                if (action == "upload")
                {
                    using var uploadStream = File.OpenRead(localPath);
                    sftp.UploadFile(uploadStream, remotePath, options.Overwrite);
                    var bytes = uploadStream.Length;
                    CaptureSuccess(into, context, bytes);
                    context.EmitOutput($"Sftp: uploaded {bytes} bytes to {host}:{port} ({remotePath})", ScriptOutputType.Debug);
                }
                else
                {
                    var fileMode = options.Overwrite ? FileMode.Create : FileMode.CreateNew;
                    using var downloadStream = new FileStream(localPath, fileMode, FileAccess.Write, FileShare.None);
                    sftp.DownloadFile(remotePath, downloadStream);
                    var bytes = downloadStream.Length;
                    CaptureSuccess(into, context, bytes);
                    context.EmitOutput($"Sftp: downloaded {bytes} bytes from {host}:{port} ({remotePath})", ScriptOutputType.Debug);
                }

                return Task.FromResult(CommandResult.Ok());
            }
            catch (Exception ex)
            {
                return Task.FromResult(ApplyOnError(step, $"Sftp error: {ex.Message}"));
            }
        }

        private static string ResolveLocalPath(ScriptContext context, string? value)
        {
            var substituted = context.SubstituteVariables(value ?? string.Empty);
            return Environment.ExpandEnvironmentVariables(substituted).Trim();
        }

        private static void ResolveEndpoint(
            SftpOptions options,
            ScriptContext context,
            out string host,
            out int port,
            out string username,
            out string password)
        {
            var rawHost = context.SubstituteVariables(options.Host ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawHost))
                rawHost = context.GetVariableString("Host_IP");

            host = rawHost;
            port = options.Port ?? 0;
            ParseHostWithOptionalPort(rawHost, ref host, ref port);

            if (port <= 0)
                port = 22;

            username = context.SubstituteVariables(options.Username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
                username = context.GetVariableString("username").Trim();

            password = context.SubstituteVariables(options.Password ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(password))
                password = context.GetVariableString("password").Trim();
        }

        private static void ParseHostWithOptionalPort(string hostValue, ref string host, ref int port)
        {
            if (string.IsNullOrWhiteSpace(hostValue))
                return;

            var idx = hostValue.LastIndexOf(':');
            if (idx <= 0 || idx == hostValue.Length - 1)
                return;

            var hostPart = hostValue[..idx];
            var portPart = hostValue[(idx + 1)..];
            if (!int.TryParse(portPart, out var parsedPort))
                return;

            host = hostPart;
            if (port <= 0)
                port = parsedPort;
        }

        private static void CaptureSuccess(string? into, ScriptContext context, long bytes)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, "success");
            context.SetVariable(into + "_bytes", bytes);
        }

        private static void CaptureFailure(string? into, ScriptContext context)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, "failure");
            context.SetVariable(into + "_bytes", 0);
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
        {
            if (string.Equals(step.OnError, "continue", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Suppressed(message);

            return CommandResult.Fail(message);
        }
    }
}
