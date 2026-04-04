using FluentAssertions;
using SSH_Helper.Services;
using System.IO;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class UpdateServiceTempPathTests
{
    [Fact]
    public void BuildUpdateTempDirectory_UsesExecutableFileNameStem()
    {
        var path = UpdateService.BuildUpdateTempDirectory(
            processPath: @"C:\Apps\SSH_Helper_Portable.exe",
            tempRoot: @"C:\Temp");

        path.Should().StartWith(@"C:\Temp\SSH_Helper_Update_SSH_Helper_Portable_");
        Path.GetFileName(path).Should().MatchRegex("^SSH_Helper_Update_SSH_Helper_Portable_[0-9a-f]{8}$");
    }

    [Fact]
    public void BuildUpdateTempDirectory_EmptyProcessPath_UsesFallbackToken()
    {
        var path = UpdateService.BuildUpdateTempDirectory(
            processPath: "",
            tempRoot: @"C:\Temp");

        path.Should().StartWith(@"C:\Temp\SSH_Helper_Update_unknown_");
        Path.GetFileName(path).Should().MatchRegex("^SSH_Helper_Update_unknown_[0-9a-f]{8}$");
    }

    [Fact]
    public void BuildUpdateTempDirectory_SameExeNameDifferentPaths_UsesDifferentDirectories()
    {
        var first = UpdateService.BuildUpdateTempDirectory(
            processPath: @"C:\Apps\A\SSH_Helper_Portable.exe",
            tempRoot: @"C:\Temp");

        var second = UpdateService.BuildUpdateTempDirectory(
            processPath: @"D:\Portable\SSH_Helper_Portable.exe",
            tempRoot: @"C:\Temp");

        first.Should().NotBe(second);
    }
}
