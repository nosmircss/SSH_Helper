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
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Sftp == null)
                return CommandResult.Fail("Sftp command has no options");

            var options = step.Sftp;
            var into = options.Into;
            CaptureFailure(into, context);

            var action = context.SubstituteVariables(options.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (action != "upload" && action != "download")
                return ApplyOnError(step, "Sftp requires 'action' to be upload or download");

            var localPath = ResolveLocalPath(context, options.LocalPath);
            var remotePath = context.SubstituteVariables(options.RemotePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(localPath))
                return ApplyOnError(step, "Sftp requires 'local_path'");
            if (string.IsNullOrWhiteSpace(remotePath))
                return ApplyOnError(step, "Sftp requires 'remote_path'");

            var timeoutSeconds = options.Timeout > 0 ? options.Timeout : 120;

            if (action == "download")
            {
                if (!options.Overwrite && File.Exists(localPath))
                {
                    return ApplyOnError(step, $"Sftp download destination already exists: {localPath}");
                }

                var localDir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrWhiteSpace(localDir))
                    Directory.CreateDirectory(localDir);
            }
            else
            {
                if (!File.Exists(localPath))
                    return ApplyOnError(step, $"Sftp upload source not found: {localPath}");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (host, port, username, password) = await ResolveEndpointAsync(
                    options, context, context.PortResolver, cancellationToken);
                if (string.IsNullOrWhiteSpace(host))
                    return ApplyOnError(step, "Sftp requires a host (or Host_IP in current context)");
                if (string.IsNullOrWhiteSpace(username))
                    return ApplyOnError(step, "Sftp requires username (or username in current context)");
                if (string.IsNullOrWhiteSpace(password))
                    return ApplyOnError(step, "Sftp requires password (or password in current context)");

                using var sftp = new SftpClient(host, port, username, password)
                {
                    OperationTimeout = TimeSpan.FromSeconds(timeoutSeconds)
                };

                sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                sftp.Connect();

                if (action == "upload" && !options.Overwrite && sftp.Exists(remotePath))
                {
                    return ApplyOnError(step, $"Sftp upload destination already exists: {remotePath}");
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

                return CommandResult.Ok();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                return ApplyOnError(step, $"Sftp error: {ex.Message}");
            }
        }

        private static string ResolveLocalPath(ScriptContext context, string? value)
        {
            var substituted = context.SubstituteVariables(value ?? string.Empty);
            return Environment.ExpandEnvironmentVariables(substituted).Trim();
        }

        internal static async Task<(string Host, int Port, string Username, string Password)> ResolveEndpointAsync(
            SftpOptions options,
            ScriptContext context,
            ConnectionPortResolver resolver,
            CancellationToken cancellationToken)
        {
            var rawHost = context.SubstituteVariables(options.Host ?? string.Empty).Trim();
            var useCurrentPolicy = string.IsNullOrWhiteSpace(rawHost) && !options.Port.HasValue &&
                context.CurrentHost != null;
            if (useCurrentPolicy)
            {
                var effective = await context.ResolveCurrentHostAsync(resolver, cancellationToken);
                rawHost = effective.IpAddress;
            }
            if (string.IsNullOrWhiteSpace(rawHost))
                rawHost = context.CurrentHost?.IpAddress ?? context.GetVariableString("Host_IP");

            var host = rawHost;
            var port = options.Port ?? (useCurrentPolicy ? context.CurrentHost!.Port : 0);
            ParseHostWithOptionalPort(rawHost, ref host, ref port);

            if (port <= 0)
                port = 22;

            var username = context.SubstituteVariables(options.Username ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
                username = context.GetVariableString("username").Trim();

            var password = context.SubstituteVariables(options.Password ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(password))
                password = context.GetVariableString("password").Trim();
            return (host, port, username, password);
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
            => CommandResult.ApplyOnError(step, message);
    }
}
