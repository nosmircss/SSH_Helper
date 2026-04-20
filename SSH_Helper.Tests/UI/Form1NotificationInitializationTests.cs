using System.Reflection;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Notifications;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1NotificationInitializationTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1NotificationInitializationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1NotificationInitializationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void InitializeNotifications_WhenProfileNotificationsAreDisabled_StillWiresToastCapableService()
    {
        using var form = new global::SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        PointFormAtTemporaryConfig(form, new AppConfiguration
        {
            Notifications = new NotificationSettings
            {
                Enabled = false
            }
        });

        InvokeMethod(form, "InitializeNotifications");

        var notificationService = GetFieldValue<NotificationService>(form, "_notificationService");
        notificationService.Should().NotBeNull("toast notifications should still be available without external notification profiles");

        var sshService = GetRequiredField<global::SSH_Helper.Services.SshExecutionService>(form, "_sshService");
        sshService.NotificationService.Should().BeSameAs(notificationService);
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

    private void PointFormAtTemporaryConfig(global::SSH_Helper.Form1 form, AppConfiguration config)
    {
        var configService = GetRequiredField<ConfigurationService>(form, "_configService");
        var configPathField = typeof(ConfigurationService).GetField("_configFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        configPathField.Should().NotBeNull();

        var configPath = Path.Combine(_testDirectory, "config.json");
        configPathField!.SetValue(configService, configPath);
        configService.Save(config);
    }

    private static T GetRequiredField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static T? GetFieldValue<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        return field!.GetValue(instance) as T;
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }
}
