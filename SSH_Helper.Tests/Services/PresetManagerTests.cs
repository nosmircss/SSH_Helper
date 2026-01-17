using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Tests for the PresetManager service.
/// Uses real ConfigurationService - tests against actual application behavior.
/// Note: These tests will use the real app config location, so they run as integration tests.
/// </summary>
public class PresetManagerTests
{
    private readonly ConfigurationService _configService;
    private readonly PresetManager _presetManager;

    public PresetManagerTests()
    {
        _configService = new ConfigurationService();
        _presetManager = new PresetManager(_configService);
    }

    #region Load Tests

    [Fact]
    public void Load_RaisesPresetsChangedEvent()
    {
        bool eventRaised = false;
        _presetManager.PresetsChanged += (s, e) => eventRaised = true;

        _presetManager.Load();

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Load_PopulatesPresetsCollection()
    {
        _presetManager.Load();

        // After loading, there should be presets (either existing or default)
        _presetManager.Presets.Should().NotBeNull();
    }

    #endregion

    #region Get Tests

    [Fact]
    public void Get_NonExistingPreset_ReturnsNull()
    {
        _presetManager.Load();

        var preset = _presetManager.Get("NonExistentPresetName_" + Guid.NewGuid());

        preset.Should().BeNull();
    }

    #endregion

    #region Save Tests

    [Fact]
    public void Save_NewPreset_AddsToCollection()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var newPreset = new PresetInfo { Commands = "test command" };

        try
        {
            _presetManager.Save(uniqueName, newPreset);

            _presetManager.Presets.Should().ContainKey(uniqueName);
            _presetManager.Get(uniqueName)!.Commands.Should().Be("test command");
        }
        finally
        {
            // Cleanup
            _presetManager.Delete(uniqueName);
        }
    }

    [Fact]
    public void Save_EmptyName_ThrowsArgumentException()
    {
        _presetManager.Load();
        var preset = new PresetInfo { Commands = "test" };

        var action = () => _presetManager.Save("", preset);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_WhitespaceName_ThrowsArgumentException()
    {
        _presetManager.Load();
        var preset = new PresetInfo { Commands = "test" };

        var action = () => _presetManager.Save("   ", preset);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_RaisesPresetsChangedEvent()
    {
        _presetManager.Load();
        bool eventRaised = false;
        _presetManager.PresetsChanged += (s, e) => eventRaised = true;
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var newPreset = new PresetInfo { Commands = "test command" };

        try
        {
            _presetManager.Save(uniqueName, newPreset);

            eventRaised.Should().BeTrue();
        }
        finally
        {
            _presetManager.Delete(uniqueName);
        }
    }

    #endregion

    #region Rename Tests

    [Fact]
    public void Rename_NonExistentPreset_ReturnsFalse()
    {
        _presetManager.Load();

        var result = _presetManager.Rename("NonExistent_" + Guid.NewGuid(), "NewName");

        result.Should().BeFalse();
    }

    [Fact]
    public void Rename_EmptyNewName_ReturnsFalse()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "test" });

        try
        {
            var result = _presetManager.Rename(uniqueName, "");

            result.Should().BeFalse();
        }
        finally
        {
            _presetManager.Delete(uniqueName);
        }
    }

    [Fact]
    public void Rename_ValidRename_ReturnsTrue()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var newName = "RenamedPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "test" });

        try
        {
            var result = _presetManager.Rename(uniqueName, newName);

            result.Should().BeTrue();
            _presetManager.Presets.Should().ContainKey(newName);
            _presetManager.Presets.Should().NotContainKey(uniqueName);
        }
        finally
        {
            _presetManager.Delete(newName);
        }
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_NonExistentPreset_ReturnsFalse()
    {
        _presetManager.Load();

        var result = _presetManager.Delete("NonExistent_" + Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void Delete_ExistingPreset_ReturnsTrue()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "test" });

        var result = _presetManager.Delete(uniqueName);

        result.Should().BeTrue();
        _presetManager.Presets.Should().NotContainKey(uniqueName);
    }

    #endregion

    #region Duplicate Tests

    [Fact]
    public void Duplicate_NonExistentPreset_ThrowsArgumentException()
    {
        _presetManager.Load();

        var action = () => _presetManager.Duplicate("NonExistent_" + Guid.NewGuid());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Duplicate_ExistingPreset_CreatesNewPreset()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "original command", Timeout = 45 });

        try
        {
            var newName = _presetManager.Duplicate(uniqueName);

            newName.Should().StartWith(uniqueName);
            _presetManager.Presets.Should().ContainKey(newName);
            _presetManager.Get(newName)!.Commands.Should().Be("original command");
            _presetManager.Get(newName)!.Timeout.Should().Be(45);

            // Cleanup the duplicate
            _presetManager.Delete(newName);
        }
        finally
        {
            _presetManager.Delete(uniqueName);
        }
    }

    #endregion

    #region GetUniqueName Tests

    [Fact]
    public void GetUniqueName_NoConflict_ReturnsSameName()
    {
        _presetManager.Load();
        var uniqueName = "UniquePreset_" + Guid.NewGuid().ToString("N");

        var name = _presetManager.GetUniqueName(uniqueName);

        name.Should().Be(uniqueName);
    }

    [Fact]
    public void GetUniqueName_WithConflict_AppendsNumber()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "test" });

        try
        {
            var name = _presetManager.GetUniqueName(uniqueName);

            name.Should().Be($"{uniqueName}_1");
        }
        finally
        {
            _presetManager.Delete(uniqueName);
        }
    }

    #endregion

    #region Export/Import Tests

    [Fact]
    public void Export_NonExistentPreset_ThrowsArgumentException()
    {
        _presetManager.Load();

        var action = () => _presetManager.Export("NonExistent_" + Guid.NewGuid());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Export_ExistingPreset_ReturnsEncodedString()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "test command" });

        try
        {
            var exported = _presetManager.Export(uniqueName);

            exported.Should().StartWith(uniqueName + "_");
        }
        finally
        {
            _presetManager.Delete(uniqueName);
        }
    }

    [Fact]
    public void Import_EmptyString_ThrowsArgumentException()
    {
        _presetManager.Load();

        var action = () => _presetManager.Import("");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Import_InvalidFormat_ThrowsFormatException()
    {
        _presetManager.Load();

        var action = () => _presetManager.Import("nounderscore");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Import_ValidEncodedString_ImportsPreset()
    {
        _presetManager.Load();
        var uniqueName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.Save(uniqueName, new PresetInfo { Commands = "import test" });

        try
        {
            var exported = _presetManager.Export(uniqueName);
            _presetManager.Delete(uniqueName);

            var importedName = _presetManager.Import(exported);

            importedName.Should().Be(uniqueName);
            _presetManager.Presets.Should().ContainKey(uniqueName);
            _presetManager.Get(importedName)!.Commands.Should().Be("import test");
        }
        finally
        {
            _presetManager.Delete(uniqueName);
        }
    }

    #endregion
}
