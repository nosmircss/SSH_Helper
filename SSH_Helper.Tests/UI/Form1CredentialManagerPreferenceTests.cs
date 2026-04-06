using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1CredentialManagerPreferenceTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1CredentialManagerPreferenceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1CredentialManagerPreferenceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void BuildApplicationState_StoresHostPasswordsAndStripsPasswordField_WhenCheckboxSettingIsOff()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        PointFormAtTemporaryConfig(form, new AppConfiguration
        {
            Credentials = new CredentialSettings
            {
                UseCredentialManager = false
            }
        });

        var credentialProvider = new RecordingCredentialProvider();
        SetField(form, "_credentialProvider", credentialProvider);

        var grid = GetField<DataGridView>(form, "dgv_variables");
        grid.Rows.Clear();
        grid.Columns.Clear();
        grid.Columns.Add(CsvManager.HostColumnName, CsvManager.HostColumnName);
        grid.Columns.Add("username", "username");
        grid.Columns.Add("password", "password");

        var rowIndex = grid.Rows.Add();
        grid.Rows[rowIndex].Cells[CsvManager.HostColumnName].Value = "10.20.30.40";
        grid.Rows[rowIndex].Cells["username"].Value = "netops";
        grid.Rows[rowIndex].Cells["password"].Value = "top-secret";

        var state = (ApplicationState)InvokeMethod(form, "BuildApplicationState")!;

        state.Hosts.Should().HaveCount(1);
        state.Hosts[0]["password"].Should().BeEmpty();

        credentialProvider.TryGetPassword(
            CredentialTargets.HostPasswordTarget("10.20.30.40", "netops"),
            out _,
            out var storedPassword).Should().BeTrue();
        storedPassword.Should().Be("top-secret");
    }

    [WinFormsFact]
    public void TryLoadDefaultPassword_DoesNotHydrateMainPassword_WhenCheckboxSettingIsOff()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        PointFormAtTemporaryConfig(form, new AppConfiguration
        {
            Credentials = new CredentialSettings
            {
                UseCredentialManager = false
            }
        });

        var credentialProvider = new RecordingCredentialProvider();
        credentialProvider.SavePassword(
            CredentialTargets.DefaultPasswordTarget,
            "operator",
            "stored-secret");
        SetField(form, "_credentialProvider", credentialProvider);

        var tsbPassword = GetField<ToolStripTextBox>(form, "tsbPassword");
        var txtPassword = GetField<TextBox>(form, "txtPassword");
        tsbPassword.Text = string.Empty;
        txtPassword.Text = string.Empty;

        InvokeMethod(form, "TryLoadDefaultPassword");

        tsbPassword.Text.Should().BeEmpty();
        txtPassword.Text.Should().BeEmpty();
    }

    [WinFormsFact]
    public void StoreDefaultPassword_DoesNotPersistMainPassword_WhenCheckboxSettingIsOff()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        PointFormAtTemporaryConfig(form, new AppConfiguration
        {
            Credentials = new CredentialSettings
            {
                UseCredentialManager = false
            }
        });

        var credentialProvider = new RecordingCredentialProvider();
        SetField(form, "_credentialProvider", credentialProvider);

        var tsbUsername = GetField<ToolStripTextBox>(form, "tsbUsername");
        var tsbPassword = GetField<ToolStripTextBox>(form, "tsbPassword");
        tsbUsername.Text = "operator";
        tsbPassword.Text = "do-not-save";

        InvokeMethod(form, "StoreDefaultPassword");

        credentialProvider.TryGetPassword(
            CredentialTargets.DefaultPasswordTarget,
            out _,
            out _).Should().BeFalse();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
        }
    }

    private void PointFormAtTemporaryConfig(SSH_Helper.Form1 form, AppConfiguration config)
    {
        var configService = GetField<ConfigurationService>(form, "_configService");
        var configPathField = typeof(ConfigurationService).GetField("_configFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        configPathField.Should().NotBeNull();

        var configPath = Path.Combine(_testDirectory, "config.json");
        configPathField!.SetValue(configService, configPath);
        configService.Save(config);
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static void SetField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        field!.SetValue(instance, value);
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }

    private sealed class RecordingCredentialProvider : ICredentialProvider
    {
        private readonly Dictionary<string, (string Username, string Password)> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;

        public bool TryGetPassword(string target, out string username, out string password)
        {
            if (_store.TryGetValue(target, out var stored))
            {
                username = stored.Username;
                password = stored.Password;
                return true;
            }

            username = string.Empty;
            password = string.Empty;
            return false;
        }

        public bool SavePassword(string target, string username, string password, string? comment = null)
        {
            _store[target] = (username, password);
            return true;
        }

        public bool DeletePassword(string target)
        {
            return _store.Remove(target);
        }
    }
}
