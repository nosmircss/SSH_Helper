using System.Reflection;
using System.Runtime.InteropServices;
using ScintillaNET;

namespace SSH_Helper.Utilities
{
    internal static class ScintillaNativeBootstrap
    {
        private const string SupportedRuntime = "win-x64";
        private static readonly string ScintillaPackageVersion =
            typeof(ScintillaNativeLibrary).Assembly.GetName().Version?.ToString() ?? "unknown";
        private const string ScintillaResourceName = "SSH_Helper.Resources.Scintilla.win-x64.Scintilla.dll";
        private const string LexillaResourceName = "SSH_Helper.Resources.Scintilla.win-x64.Lexilla.dll";

        public static void ConfigureSatelliteDirectory()
        {
            var runtimeIdentifier = ResolveRuntimeIdentifier();
            if (runtimeIdentifier == null)
            {
                return;
            }

            var packagedNativeDirectory = GetPackagedNativeDirectory(runtimeIdentifier);
            if (packagedNativeDirectory != null)
            {
                ScintillaNativeLibrary.SatelliteDirectory = packagedNativeDirectory;
                return;
            }

            if (!string.Equals(runtimeIdentifier, SupportedRuntime, StringComparison.Ordinal))
            {
                return;
            }

            var extractedNativeDirectory = ExtractEmbeddedNativeLibraries();
            ScintillaNativeLibrary.SatelliteDirectory = extractedNativeDirectory;
        }

        private static string? ResolveRuntimeIdentifier()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.X64 => "win-x64",
                System.Runtime.InteropServices.Architecture.X86 => "win-x86",
                System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
                _ => null
            };
        }

        private static string? GetPackagedNativeDirectory(string runtimeIdentifier)
        {
            var nativeDirectory = Path.Combine(AppContext.BaseDirectory, "runtimes", runtimeIdentifier, "native");
            return HasRequiredNativeLibraries(nativeDirectory) ? nativeDirectory : null;
        }

        private static string ExtractEmbeddedNativeLibraries()
        {
            Exception? lastException = null;
            foreach (var rootDirectory in EnumerateExtractionRoots())
            {
                var extractionDirectory = Path.Combine(
                    rootDirectory,
                    "scintilla-native",
                    ScintillaPackageVersion,
                    SupportedRuntime);

                try
                {
                    Directory.CreateDirectory(extractionDirectory);
                    ExtractEmbeddedResource(ScintillaResourceName, Path.Combine(extractionDirectory, "Scintilla.dll"));
                    ExtractEmbeddedResource(LexillaResourceName, Path.Combine(extractionDirectory, "Lexilla.dll"));
                    return extractionDirectory;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                }
            }

            throw new InvalidOperationException(
                "Unable to extract embedded Scintilla native libraries to a writable location.",
                lastException);
        }

        private static IEnumerable<string> EnumerateExtractionRoots()
        {
            string? appFolder = null;
            try
            {
                appFolder = AppDataPaths.GetAppFolder();
            }
            catch
            {
                // Fall back to temp-only extraction when app storage is unavailable.
            }

            if (!string.IsNullOrWhiteSpace(appFolder))
            {
                yield return appFolder;
            }

            var tempDirectory = Path.GetTempPath();
            if (!string.IsNullOrWhiteSpace(tempDirectory))
            {
                yield return Path.Combine(tempDirectory, "SSH_Helper");
            }
        }

        private static void ExtractEmbeddedResource(string resourceName, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                throw new InvalidOperationException($"Embedded Scintilla resource '{resourceName}' was not found.");
            }

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            resourceStream.CopyTo(fileStream);
        }

        private static bool HasRequiredNativeLibraries(string nativeDirectory)
        {
            return File.Exists(Path.Combine(nativeDirectory, "Scintilla.dll")) &&
                   File.Exists(Path.Combine(nativeDirectory, "Lexilla.dll"));
        }
    }
}
