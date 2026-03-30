using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public sealed class FlowCanvasDistLocatorTests
{
    [Fact]
    public void FlowCanvasDistResources_AreEmbeddedWithExpectedPrefix()
    {
        var resources = typeof(FlowCanvasDistLocator).Assembly.GetManifestResourceNames();

        resources.Should().Contain(name => name.StartsWith("SSH_Helper.Resources.FlowCanvasDist/", StringComparison.Ordinal));
        resources.Should().Contain("SSH_Helper.Resources.FlowCanvasDist/index.html");
    }

    [Fact]
    public void ExtractEmbeddedDistForTests_WritesIndexHtmlToAppData()
    {
        var distPath = FlowCanvasDistLocator.ExtractEmbeddedDistForTests();

        distPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(Path.Combine(distPath!, "index.html")).Should().BeTrue();
    }

    [Fact]
    public void ResolveDistPath_UsesEmbeddedDist_WhenPackagedPathsAreMissing()
    {
        using var temp = new TempDirectoryScope();
        var exeDir = temp.CreateDirectory("exe");
        var embeddedDist = temp.CreateDistDirectory("embedded");

        var result = FlowCanvasDistLocator.ResolveDistPath(
            exeDir,
            _ => null,
            () => embeddedDist);

        result.DistPath.Should().Be(embeddedDist);
        result.SearchedPaths.Should().Contain(Path.Combine(exeDir, "FlowCanvas", "dist"));
        result.SearchedPaths.Should().Contain("(no project root found)");
        result.SearchedPaths.Should().Contain(embeddedDist);
    }

    [Fact]
    public void ResolveDistPath_PrefersExecutableRelativeDist_WhenPresent()
    {
        using var temp = new TempDirectoryScope();
        var exeDir = temp.CreateDirectory("exe");
        var exeDist = temp.CreateDistDirectory("exe", "FlowCanvas", "dist");
        var projectRoot = temp.CreateDirectory("repo");
        temp.CreateDistDirectory("repo", "FlowCanvas", "dist");
        var embeddedDist = temp.CreateDistDirectory("embedded");

        var result = FlowCanvasDistLocator.ResolveDistPath(
            exeDir,
            _ => projectRoot,
            () => embeddedDist);

        result.DistPath.Should().Be(exeDist);
    }

    [Fact]
    public void ResolveDistPath_ReturnsNull_WhenNoCandidateContainsIndexHtml()
    {
        using var temp = new TempDirectoryScope();
        var exeDir = temp.CreateDirectory("exe");
        var missingIndexDist = temp.CreateDirectory("embedded-no-index");

        var result = FlowCanvasDistLocator.ResolveDistPath(
            exeDir,
            _ => null,
            () => missingIndexDist);

        result.DistPath.Should().BeNull();
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ssh-helper-flowcanvas-tests", Guid.NewGuid().ToString("N"));

        public TempDirectoryScope()
        {
            Directory.CreateDirectory(_root);
        }

        public string CreateDirectory(params string[] parts)
        {
            var path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateDistDirectory(params string[] parts)
        {
            var path = CreateDirectory(parts);
            File.WriteAllText(Path.Combine(path, "index.html"), "<!doctype html><html></html>");
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
