using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1HistorySelectionLifecycleTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1HistorySelectionLifecycleTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1HistorySelectionLifecycleTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void ArmHistorySelectionOnIdle_AfterFormDisposal_DoesNotTouchDisposedOutputControls()
    {
        using var form = CreateLoadedForm(new AppConfiguration());

        var outputHistory = GetField<BindingList<HistoryListItem>>(form, "_outputHistory");
        var historyList = GetField<HistoryListBox>(form, "lstOutput");

        outputHistory.Add(new HistoryListItem("missing-entry", "Missing Entry"));
        historyList.SelectedIndex = 0;

        SetField(form, "_historySelectionHandlingEnabled", false);
        SetField(form, "_historySelectionArmPending", true);

        form.Dispose();

        var action = () => InvokeMethod(form, "ArmHistorySelectionOnIdle", null, EventArgs.Empty);

        action.Should().NotThrow("startup history hydration should no-op once the form has been disposed");
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

    private SSH_Helper.Form1 CreateLoadedForm(AppConfiguration config)
    {
        var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();
        PointFormAtTemporaryConfig(form, config);
        return form;
    }

    private void PointFormAtTemporaryConfig(SSH_Helper.Form1 form, AppConfiguration config)
    {
        var configService = GetField<ConfigurationService>(form, "_configService");
        var configPathField = typeof(ConfigurationService).GetField("_configFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        configPathField.Should().NotBeNull();

        var configPath = Path.Combine(_testDirectory, "config.json");
        configPathField!.SetValue(configService, configPath);
        configService.Save(config);

        var presetManager = GetField<PresetManager>(form, "_presetManager");
        presetManager.Load(config);

        InvokeMethod(form, "RefreshPresetList", true, null, null, config);
        InvokeMethod(form, "RefreshFavoritesList", new object?[] { null });
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static void SetField<T>(object instance, string fieldName, T value)
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
}
