using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using FluentAssertions;
using Moq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Tests.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ConnectionPortHostBuilderTests
{
    [WinFormsTheory]
    [InlineData("", "", false, false, "router", "default-user", 2200)]
    [InlineData("", "", true, false, "router", "default-user", 2200)]
    [InlineData("", "", false, true, "router", "default-user", 222)]
    [InlineData("2201", "row-user", false, false, "router:2201", "row-user", 2201)]
    [InlineData("22", "", false, false, "router", "default-user", 22)]
    public async Task ManualBuilder_KeepsLegacyCredentialKeyBeforeConfigAndRuntimeSelection(
        string rowPort, string rowUsername, bool store, bool discovery,
        string legacyHost, string legacyUser, int expectedPort)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"port-credential-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var config = new ConfigurationService(Path.Combine(directory, "config.json"));
            config.Update(c => c.SshConfig.EnableSshConfig = true);
            var env = new EnvironmentService(config);
            if (discovery) env.UpdateActiveEnvironmentVariable("port", "22,222");
            var form = (Form1)RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            using var grid = new DataGridView { AllowUserToAddRows = false };
            foreach (var column in new[] { "Host_IP", "port", "username", "password" })
                grid.Columns.Add(column, column);
            var index = grid.Rows.Add("router", rowPort, rowUsername, store ? "synthetic-password" : "");
            var sshConfig = new Mock<SshConfigService>();
            sshConfig.Setup(s => s.GetHostConfig("router", null))
                .Returns(new SshHostConfig { Port = 2200, User = "ssh-user" });
            var credentials = new Mock<ICredentialProvider>(MockBehavior.Strict);
            credentials.Setup(c => c.IsAvailable).Returns(true);
            var expectedTarget = CredentialTargets.HostPasswordTarget(legacyHost, legacyUser);
            if (store)
                credentials.Setup(c => c.SavePassword(expectedTarget, legacyUser, "synthetic-password", null)).Returns(true);
            else
                credentials.Setup(c => c.TryGetPassword(expectedTarget, out It.Ref<string>.IsAny, out It.Ref<string>.IsAny))
                    .Returns((string target, out string username, out string password) =>
                    {
                        username = legacyUser;
                        password = "synthetic-password";
                        return true;
                    });
            Set(form, "_configService", config);
            Set(form, "_environmentService", env);
            Set(form, "_sshConfigService", sshConfig.Object);
            Set(form, "_credentialProvider", credentials.Object);
            Set(form, "dgv_variables", grid);
            using var usernameControl = new ToolStripTextBox { Text = "default-user" };
            Set(form, "tsbUsername", usernameControl);
            var host = ((IEnumerable<HostConnection>)Invoke(form, "GetHostConnections",
                new[] { grid.Rows[index] })).Single();
            var resolved = await new ConnectionPortResolver(new RecordingPortProbe()).ResolveAsync(host, null, _ => { }, default);
            resolved.Port.Should().Be(expectedPort);
            resolved.Username.Should().Be(string.IsNullOrEmpty(rowUsername) ? "ssh-user" : rowUsername);
            resolved.Password.Should().Be("synthetic-password");
            resolved.EventHost.Should().BeSameAs(host);
            credentials.VerifyAll();
            if (discovery) host.Variables["port"].Should().Be("22,222");
        }
        finally { Directory.Delete(directory, true); }
    }

    [WinFormsTheory]
    [InlineData("router", " ", "22,222,2222", 22)]
    [InlineData("router", "22", "22", 22)]
    [InlineData("router", "2200,2222", "2200,2222", 22)]
    [InlineData("router:22", "bad,list", "22", 22)]
    [InlineData("router", "2200", "2200", 2200)]
    public void ManualBuilderAndPreviewSharePolicyWithoutCredentialSideEffects(
        string address, string rowPort, string expected, int port)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"port-builder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var config = new ConfigurationService(Path.Combine(directory, "config.json"));
            var env = new EnvironmentService(config);
            env.UpdateActiveEnvironmentVariable("port", "22,222,2222");
            env.UpdateActiveEnvironmentVariable("port_check_timeout", "3");
            // Deliberately skip Form1 startup: no actual settings, scheduler, vault, licensing or windows.
            var form = (Form1)RuntimeHelpers.GetUninitializedObject(typeof(Form1));
            using var grid = new DataGridView { AllowUserToAddRows = false };
            foreach (var column in new[] { "Host_IP", "PoRt", "port_check", "username" })
                grid.Columns.Add(column, column);
            var index = grid.Rows.Add(address, rowPort, "tcp", "user");
            Set(form, "_configService", config);
            Set(form, "_environmentService", env);
            Set(form, "_sshConfigService", new SshConfigService());
            Set(form, "dgv_variables", grid);
            Set(form, "tsbUsername", new ToolStripTextBox { Text = "default-user" });
            var credentials = new Mock<ICredentialProvider>(MockBehavior.Strict);
            credentials.Setup(c => c.IsAvailable).Returns(false);
            Set(form, "_credentialProvider", credentials.Object);
            var row = grid.Rows[index];
            var preview = Invoke(form, "BuildHostPayload", row);
            var variables = (Dictionary<string, string>)preview.GetType().GetProperty("variables")!.GetValue(preview)!;
            variables["port"].Should().Be(expected);
            // Pure preview does not even ask about credential availability.
            credentials.Verify(c => c.IsAvailable, Times.Never);
            var hosts = ((IEnumerable<HostConnection>)Invoke(form, "GetHostConnections",
                new[] { row })).ToList();
            hosts.Single().PortPolicy!.RawPort.Should().Be(expected);
            hosts.Single().Port.Should().Be(port);
            hosts.Single().PortPolicy!.RawTimeout.Should().Be("3");
            row.Cells["PoRt"].Value.Should().Be(rowPort);
            env.GetActiveEnvironmentVariables()["port"].Should().Be("22,222,2222");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Set(object target, string field, object value) =>
        typeof(Form1).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static object Invoke(object target, string method, object value) =>
        typeof(Form1).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, new[] { value })!;
}
