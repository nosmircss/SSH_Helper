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

    #region Folder Tests

    [Fact]
    public void CreateFolder_NewFolder_ReturnsTrue()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        try
        {
            var result = _presetManager.CreateFolder(folderName);

            result.Should().BeTrue();
            _presetManager.Folders.Should().ContainKey(folderName);
        }
        finally
        {
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void CreateFolder_DuplicateName_ReturnsFalse()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);

        try
        {
            var result = _presetManager.CreateFolder(folderName);

            result.Should().BeFalse();
        }
        finally
        {
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void CreateFolder_EmptyName_ThrowsArgumentException()
    {
        _presetManager.Load();

        var action = () => _presetManager.CreateFolder("");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RenameFolder_ValidRename_ReturnsTrue()
    {
        _presetManager.Load();
        var oldName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var newName = "RenamedFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(oldName);

        try
        {
            var result = _presetManager.RenameFolder(oldName, newName);

            result.Should().BeTrue();
            _presetManager.Folders.Should().ContainKey(newName);
            _presetManager.Folders.Should().NotContainKey(oldName);
        }
        finally
        {
            _presetManager.DeleteFolder(newName, deletePresets: true);
        }
    }

    [Fact]
    public void RenameFolder_UpdatesPresetsInFolder()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var newFolderName = "RenamedFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test", Folder = folderName });

        try
        {
            _presetManager.RenameFolder(folderName, newFolderName);

            var preset = _presetManager.Get(presetName);
            preset!.Folder.Should().Be(newFolderName);
        }
        finally
        {
            _presetManager.Delete(presetName);
            _presetManager.DeleteFolder(newFolderName, deletePresets: true);
        }
    }

    [Fact]
    public void DeleteFolder_MovesPresetsToRoot()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test", Folder = folderName });

        try
        {
            _presetManager.DeleteFolder(folderName, deletePresets: false);

            _presetManager.Folders.Should().NotContainKey(folderName);
            _presetManager.Presets.Should().ContainKey(presetName);
            var preset = _presetManager.Get(presetName);
            preset!.Folder.Should().BeNull();
        }
        finally
        {
            _presetManager.Delete(presetName);
        }
    }

    [Fact]
    public void DeleteFolder_WithDeletePresets_DeletesPresets()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test", Folder = folderName });

        _presetManager.DeleteFolder(folderName, deletePresets: true);

        _presetManager.Folders.Should().NotContainKey(folderName);
        _presetManager.Presets.Should().NotContainKey(presetName);
    }

    [Fact]
    public void MovePresetToFolder_MovesToFolder()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test" });

        try
        {
            var result = _presetManager.MovePresetToFolder(presetName, folderName);

            result.Should().BeTrue();
            var preset = _presetManager.Get(presetName);
            preset!.Folder.Should().Be(folderName);
        }
        finally
        {
            _presetManager.Delete(presetName);
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void MovePresetToFolder_NullFolder_MovesToRoot()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test", Folder = folderName });

        try
        {
            var result = _presetManager.MovePresetToFolder(presetName, null);

            result.Should().BeTrue();
            var preset = _presetManager.Get(presetName);
            preset!.Folder.Should().BeNull();
        }
        finally
        {
            _presetManager.Delete(presetName);
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void GetPresetsInFolder_ReturnsCorrectPresets()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var preset1 = "TestPreset1_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var preset2 = "TestPreset2_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var rootPreset = "RootPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        _presetManager.CreateFolder(folderName);
        _presetManager.Save(preset1, new PresetInfo { Commands = "test1", Folder = folderName });
        _presetManager.Save(preset2, new PresetInfo { Commands = "test2", Folder = folderName });
        _presetManager.Save(rootPreset, new PresetInfo { Commands = "root" });

        try
        {
            var presetsInFolder = _presetManager.GetPresetsInFolder(folderName).ToList();

            presetsInFolder.Should().Contain(preset1);
            presetsInFolder.Should().Contain(preset2);
            presetsInFolder.Should().NotContain(rootPreset);
        }
        finally
        {
            _presetManager.Delete(preset1);
            _presetManager.Delete(preset2);
            _presetManager.Delete(rootPreset);
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void Export_IncludesFolder()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test", Folder = folderName });

        try
        {
            var exported = _presetManager.Export(presetName);
            _presetManager.Delete(presetName);

            var importedName = _presetManager.Import(exported);
            var importedPreset = _presetManager.Get(importedName);

            importedPreset!.Folder.Should().Be(folderName);
        }
        finally
        {
            _presetManager.Delete(presetName);
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void Import_CreatesFolder_IfMissing()
    {
        _presetManager.Load();
        var folderName = "ImportedFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var presetName = "TestPreset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);
        _presetManager.Save(presetName, new PresetInfo { Commands = "test", Folder = folderName });

        try
        {
            var exported = _presetManager.Export(presetName);
            _presetManager.Delete(presetName);
            _presetManager.DeleteFolder(folderName, deletePresets: true);

            // Now import - folder should be recreated
            var importedName = _presetManager.Import(exported);

            _presetManager.Folders.Should().ContainKey(folderName);
            var preset = _presetManager.Get(importedName);
            preset!.Folder.Should().Be(folderName);
        }
        finally
        {
            _presetManager.Delete(presetName);
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    [Fact]
    public void SetFolderExpanded_PersistsState()
    {
        _presetManager.Load();
        var folderName = "TestFolder_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        _presetManager.CreateFolder(folderName);

        try
        {
            _presetManager.SetFolderExpanded(folderName, false);

            var folderInfo = _presetManager.Folders[folderName];
            folderInfo.IsExpanded.Should().BeFalse();

            _presetManager.SetFolderExpanded(folderName, true);
            folderInfo = _presetManager.Folders[folderName];
            folderInfo.IsExpanded.Should().BeTrue();
        }
        finally
        {
            _presetManager.DeleteFolder(folderName, deletePresets: true);
        }
    }

    #endregion
}
