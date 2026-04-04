using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public sealed class AppDataPathsTests : IDisposable
{
    private readonly string _tempRoot;

    public AppDataPathsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"AppDataPathsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best effort test cleanup.
        }
    }

    [Fact]
    public void ResolveAppFolder_PortableBuild_ReturnsExecutableDirectory()
    {
        var exeDir = Path.Combine(_tempRoot, "portable-exe");
        var localAppData = Path.Combine(_tempRoot, "localappdata");

        Directory.CreateDirectory(exeDir);
        Directory.CreateDirectory(localAppData);

        var resolved = AppDataPaths.ResolveAppFolder(
            portableBuild: true,
            baseDirectory: exeDir,
            localAppDataDirectory: localAppData);

        resolved.Should().Be(Path.GetFullPath(exeDir));
    }

    [Fact]
    public void ResolveAppFolder_StandardBuild_ReturnsLocalAppDataAppFolder()
    {
        var exeDir = Path.Combine(_tempRoot, "standard-exe");
        var localAppData = Path.Combine(_tempRoot, "localappdata");

        Directory.CreateDirectory(exeDir);
        Directory.CreateDirectory(localAppData);

        var resolved = AppDataPaths.ResolveAppFolder(
            portableBuild: false,
            baseDirectory: exeDir,
            localAppDataDirectory: localAppData);

        resolved.Should().Be(Path.Combine(localAppData, "SSH_Helper"));
    }

    [Fact]
    public void TryEnsureFolderWritable_WritableDirectory_ReturnsTrue()
    {
        var targetDir = Path.Combine(_tempRoot, "writable");
        Directory.CreateDirectory(targetDir);

        var ok = AppDataPaths.TryEnsureFolderWritable(targetDir, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryEnsureFolderWritable_FilePath_ReturnsFalse()
    {
        var filePath = Path.Combine(_tempRoot, "not-a-directory.txt");
        File.WriteAllText(filePath, "test");

        var ok = AppDataPaths.TryEnsureFolderWritable(filePath, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
