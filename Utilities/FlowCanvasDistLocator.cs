using System.Reflection;
using System.Text;

namespace SSH_Helper.Utilities
{
    internal sealed record FlowCanvasDistResolution(string? DistPath, IReadOnlyList<string> SearchedPaths);

    internal static class FlowCanvasDistLocator
    {
        private const string EmbeddedResourcePrefix = "SSH_Helper.Resources.FlowCanvasDist/";
        private const string DistIndexFileName = "index.html";
        private static readonly object ExtractionSync = new();
        private static bool _embeddedExtractionAttempted;
        private static string? _cachedEmbeddedDistPath;

        public static FlowCanvasDistResolution ResolveDistPath()
        {
            return ResolveDistPath(
                AppDomain.CurrentDomain.BaseDirectory,
                FindProjectRoot,
                TryExtractEmbeddedDistToAppData);
        }

        internal static FlowCanvasDistResolution ResolveDistPath(
            string exeDir,
            Func<string, string?> projectRootFinder,
            Func<string?> embeddedDistResolver)
        {
            var searchedPaths = new List<string>();

            var fromExe = Path.Combine(exeDir, "FlowCanvas", "dist");
            searchedPaths.Add(fromExe);
            if (HasDistIndex(fromExe))
                return new FlowCanvasDistResolution(fromExe, searchedPaths);

            var projectRoot = projectRootFinder(exeDir);
            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                var fromProject = Path.Combine(projectRoot, "FlowCanvas", "dist");
                searchedPaths.Add(fromProject);
                if (HasDistIndex(fromProject))
                    return new FlowCanvasDistResolution(fromProject, searchedPaths);
            }
            else
            {
                searchedPaths.Add("(no project root found)");
            }

            var embeddedDist = embeddedDistResolver();
            searchedPaths.Add(embeddedDist ?? "(embedded FlowCanvas dist unavailable)");
            if (!string.IsNullOrWhiteSpace(embeddedDist) && HasDistIndex(embeddedDist))
                return new FlowCanvasDistResolution(embeddedDist, searchedPaths);

            return new FlowCanvasDistResolution(null, searchedPaths);
        }

        internal static string? ExtractEmbeddedDistForTests()
        {
            return TryExtractEmbeddedDistToAppData();
        }

        internal static string? FindProjectRoot(string startDir)
        {
            var dir = startDir;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "SSH_Helper.csproj")) ||
                    File.Exists(Path.Combine(dir, "SSH_Helper.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static bool HasDistIndex(string distPath)
        {
            return Directory.Exists(distPath) &&
                   File.Exists(Path.Combine(distPath, DistIndexFileName));
        }

        private static string? TryExtractEmbeddedDistToAppData()
        {
            lock (ExtractionSync)
            {
                if (_embeddedExtractionAttempted)
                    return _cachedEmbeddedDistPath;

                _embeddedExtractionAttempted = true;

                try
                {
                    _cachedEmbeddedDistPath = ExtractEmbeddedDistToAppData();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    _cachedEmbeddedDistPath = null;
                }

                return _cachedEmbeddedDistPath;
            }
        }

        private static string? ExtractEmbeddedDistToAppData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith(EmbeddedResourcePrefix, StringComparison.Ordinal))
                .ToArray();

            if (resourceNames.Length == 0)
                return null;

            var extractionDirectory = Path.Combine(
                AppDataPaths.GetAppFolder(),
                "flow-canvas-dist",
                GetExtractionVersionSegment(assembly));

            Directory.CreateDirectory(extractionDirectory);

            foreach (var resourceName in resourceNames)
            {
                var relative = resourceName.Substring(EmbeddedResourcePrefix.Length)
                    .TrimStart('\\', '/');
                if (string.IsNullOrWhiteSpace(relative))
                    continue;

                var normalizedRelative = relative.Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                var destinationPath = Path.Combine(extractionDirectory, normalizedRelative);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                    continue;

                Directory.CreateDirectory(destinationDirectory);
                if (File.Exists(destinationPath))
                    continue;

                using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream == null)
                    continue;

                using var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                resourceStream.CopyTo(outputStream);
            }

            return HasDistIndex(extractionDirectory) ? extractionDirectory : null;
        }

        private static string GetExtractionVersionSegment(Assembly assembly)
        {
            var buildTimestamp = assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attr => string.Equals(attr.Key, "BuildTimestamp", StringComparison.Ordinal))
                ?.Value;

            var versionToken = !string.IsNullOrWhiteSpace(buildTimestamp)
                ? buildTimestamp
                : assembly.GetName().Version?.ToString() ?? "unknown";

            return SanitizePathSegment(versionToken);
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }

            var sanitized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }
    }
}
